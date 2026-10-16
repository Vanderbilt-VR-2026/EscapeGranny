using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Put on Padlock_Root (next to Padlock, Rigidbody and XRGrabInteractable).
/// The padlock hangs from the chain by its shackle, swings a little, and can be grabbed.
/// While held it can be moved a short way (so you can reach it with the key); let go and it drifts back.
/// When the Padlock unlocks, the joint is removed and it falls with the chain.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(Padlock))]
public class HangingPadlock : MonoBehaviour
{
    [Header("Where it hangs")]
    [SerializeField] ChainBuilder chain;
    [Tooltip("0 = chain start, 1 = chain end, 0.5 = the middle link.")]
    [SerializeField, Range(0.05f, 0.95f)] float chainPosition = 0.5f;
    [Tooltip("Optional empty child at the middle of the shackle loop. If empty, the top of the padlock's mesh is used.")]
    [SerializeField] Transform hangPoint;
    [Tooltip("How far below the very top of the padlock the chain sits (meters).")]
    [SerializeField] float hangDepth = 0.012f;

    [Header("Facing")]
    [Tooltip("Turn the padlock so the chain passes through the shackle.")]
    [SerializeField] bool alignToChain = true;
    [Tooltip("If the shackle isn't threaded through the chain, try 90, 180 or 270.")]
    [SerializeField] float yawOffset = 0f;

    [Header("Feel")]
    [Tooltip("How far you can pull it away from the chain while holding it (meters).")]
    [SerializeField] float heldReach = 0.12f;
    [Tooltip("Small slack when just hanging (meters).")]
    [SerializeField] float hangingSlack = 0.008f;
    [SerializeField] float returnSpeed = 0.15f;
    [SerializeField] float swingDamping = 2f;
    [Tooltip("How much of the chain follows the padlock when you pull it (0.1 = just the nearest links, 0.5 = most of the chain).")]
    [SerializeField, Range(0.1f, 0.6f)] float chainFollowWidth = 0.3f;

    [Header("Avoid jitter")]
    [Tooltip("Drag the door here. The hanging padlock ignores its colliders so it doesn't bump the door.")]
    [SerializeField] Transform ignoreCollisionsWith;

    Rigidbody rb;
    Padlock padlock;
    XRGrabInteractable grab;
    ConfigurableJoint joint;
    Rigidbody anchorRb;
    float currentLimit;
    float targetLimit;
    bool held;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        padlock = GetComponent<Padlock>();
        grab = GetComponent<XRGrabInteractable>();
        rb.isKinematic = true;          // stay put until the chain exists
    }

    void OnEnable()
    {
        padlock.onUnlocked.AddListener(Drop);
        if (grab != null)
        {
            grab.selectEntered.AddListener(OnGrab);
            grab.selectExited.AddListener(OnRelease);
        }
    }

    void OnDisable()
    {
        padlock.onUnlocked.RemoveListener(Drop);
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnGrab);
            grab.selectExited.RemoveListener(OnRelease);
        }
    }

    IEnumerator Start()
    {
        yield return null;              // let ChainBuilder.Start run first
        Attach();
    }

    [ContextMenu("Re-attach")]
    public void Attach()
    {
        if (chain == null) { Debug.LogWarning("HangingPadlock: assign the Chain."); rb.isKinematic = false; return; }

        Vector3 anchorPos = chain.PointAt(chainPosition);

        // 1. Turn so the chain runs through the shackle.
        if (alignToChain)
        {
            Vector3 dir = chain.PointAt(chainPosition + 0.05f) - chain.PointAt(chainPosition - 0.05f);
            dir.y = 0f;
            Vector3 cur = Vector3.ProjectOnPlane(transform.right, Vector3.up);
            if (dir.sqrMagnitude > 1e-6f && cur.sqrMagnitude > 1e-6f)
            {
                float delta = Vector3.SignedAngle(cur, dir.normalized, Vector3.up) + yawOffset;
                transform.RotateAround(HangWorld(), Vector3.up, delta);
            }
        }

        // 2. Slide so the shackle sits on the chain.
        transform.position += anchorPos - HangWorld();

        // 3. Don't bump the door while hanging.
        if (ignoreCollisionsWith != null)
        {
            foreach (var mine in GetComponentsInChildren<Collider>())
                foreach (var theirs in ignoreCollisionsWith.GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(mine, theirs, true);
        }

        // 4. A fixed point on the chain, and a joint that lets the padlock swing from it.
        var anchorGo = new GameObject("PadlockAnchor");
        anchorGo.transform.SetParent(chain.transform, true);
        anchorGo.transform.position = anchorPos;
        anchorRb = anchorGo.AddComponent<Rigidbody>();
        anchorRb.isKinematic = true;

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearDamping = 0.5f;
        rb.angularDamping = swingDamping;

        joint = gameObject.AddComponent<ConfigurableJoint>();
        joint.autoConfigureConnectedAnchor = false;
        joint.connectedBody = anchorRb;
        joint.anchor = transform.InverseTransformPoint(anchorPos);
        joint.connectedAnchor = Vector3.zero;
        joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Limited;
        joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Free;
        joint.projectionMode = JointProjectionMode.PositionAndRotation;
        joint.projectionDistance = 0.01f;
        joint.enableCollision = false;

        currentLimit = targetLimit = hangingSlack;
        ApplyLimit();

        // Held objects need velocity tracking to work with a joint.
        if (grab != null)
        {
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.throwOnDetach = false;
        }
    }

    // After physics has moved the padlock, drag the chain's middle links to where the shackle now is.
    void LateUpdate()
    {
        if (joint == null || chain == null) return;
        Vector3 shackleNow = transform.TransformPoint(joint.anchor);
        Vector3 offset = shackleNow - anchorRb.position;
        chain.SetPull(chainPosition, offset, chainFollowWidth);
    }

    void Update()
    {
        if (joint == null) return;
        float speed = held ? 0.6f : returnSpeed;     // reach out quickly, drift back slowly
        float next = Mathf.MoveTowards(currentLimit, targetLimit, speed * Time.deltaTime);
        if (!Mathf.Approximately(next, currentLimit))
        {
            currentLimit = next;
            ApplyLimit();
        }
    }

    void ApplyLimit()
    {
        var l = joint.linearLimit;
        l.limit = currentLimit;
        joint.linearLimit = l;
    }

    void OnGrab(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs _)
    {
        held = true;
        targetLimit = heldReach;
    }

    void OnRelease(UnityEngine.XR.Interaction.Toolkit.SelectExitEventArgs _)
    {
        held = false;
        targetLimit = hangingSlack;
    }

    /// <summary>Called when the key unlocks the padlock. It falls off the chain.</summary>
    public void Drop()
    {
        if (joint != null) Destroy(joint);
        joint = null;
        if (anchorRb != null) Destroy(anchorRb.gameObject);
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearDamping = 0f;
        rb.angularDamping = 0.05f;
        rb.AddForce(new Vector3(0.02f, 0.01f, 0f), ForceMode.Impulse);
    }

    // Where the shackle's loop is, in world space.
    Vector3 HangWorld()
    {
        if (hangPoint != null) return hangPoint.position;

        var rends = GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return transform.position;
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return new Vector3(b.center.x, b.max.y - hangDepth, b.center.z);
    }
}