using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Put on Bomb_Root. Pull the Pin (a grabbable child) with the free hand to arm it.
/// Once armed, the fuse starts when the bomb is released (thrown). After the explosion a fresh
/// bomb appears at the hand that threw it.
/// For multiplayer, also add NetworkBomb + XRGrabNetworkInteractable: then the same bomb is reset
/// into the hand instead of a new local copy, so the other player sees it too.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody))]
public class Bomb : MonoBehaviour
{
    [Header("Pin")]
    [SerializeField] XRGrabInteractable pin;
    [SerializeField] float pullDistance = 0.08f;
    [Tooltip("Testing aid: pressing the trigger while holding the bomb also pulls the pin.")]
    [SerializeField] bool armWithTriggerFallback = false;

    [Header("Fuse")]
    [SerializeField] float fuseTime = 2.5f;

    [Header("Explosion")]
    [SerializeField] float radius = 3f;
    [SerializeField] float force = 300f;
    [SerializeField] float upwardsModifier = 0.5f;
    [SerializeField] GameObject explosionPrefab;

    [Header("Respawn")]
    [SerializeField] GameObject bombPrefab;
    [SerializeField] float respawnDelay = 0.75f;
    [SerializeField] float hoverSeconds = 5f;

    XRGrabInteractable grab;
    Rigidbody rb;
    NetworkBomb networkBomb;
    IXRSelectInteractor lastHolder;
    Vector3 pinRestLocalPos;
    Quaternion pinRestLocalRot;
    Rigidbody pinRb;
    bool pinStartKinematic, pinStartGravity;
    Vector3 startPos;
    Quaternion startRot;
    bool armed, fuseStarted, exploded, pinReleased;
    bool pinRequested;   // stops Update asking the network again while the RPC is on its way
    bool resetHere;      // true on the headset that threw the bomb; only it puts the bomb back

    bool Networked => networkBomb && networkBomb.IsReady;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
        networkBomb = GetComponent<NetworkBomb>();

        if (pin != null)
        {
            pinRestLocalPos = pin.transform.localPosition;
            pinRestLocalRot = pin.transform.localRotation;
            pinRb = pin.GetComponent<Rigidbody>();
            if (pinRb != null)
            {
                pinStartKinematic = pinRb.isKinematic;
                pinStartGravity = pinRb.useGravity;
            }

            // The pin must not physically push the bomb around.
            var pinCols = pin.GetComponentsInChildren<Collider>();
            var bombCols = GetComponentsInChildren<Collider>();
            foreach (var a in pinCols)
                foreach (var b in bombCols)
                    if (a != b) Physics.IgnoreCollision(a, b);
        }
        else
        {
            pinReleased = true;
        }
    }

    void OnEnable()
    {
        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnReleased);
        grab.activated.AddListener(OnActivated);
    }

    void OnDisable()
    {
        grab.selectEntered.RemoveListener(OnGrabbed);
        grab.selectExited.RemoveListener(OnReleased);
        grab.activated.RemoveListener(OnActivated);
    }

    void Update()
    {
        if (armed || pinRequested || pin == null || !pin.isSelected) return;

        Vector3 rest = transform.TransformPoint(pinRestLocalPos);
        if (Vector3.Distance(pin.transform.position, rest) > pullDistance)
            PullPin();
    }

    void OnGrabbed(SelectEnterEventArgs args) => lastHolder = args.interactorObject;

    void OnReleased(SelectExitEventArgs args)
    {
        lastHolder = args.interactorObject;

        if (armed && !fuseStarted)
        {
            fuseStarted = true;
            Debug.Log("Bomb thrown, fuse started");
            Invoke(nameof(Explode), fuseTime);
        }
    }

    void OnActivated(ActivateEventArgs args)
    {
        if (armWithTriggerFallback) PullPin();
    }

    void PullPin()
    {
        if (armed || pinRequested) return;
        pinRequested = true;

        // Online: tell both players, so the other side also sees the pin come out.
        if (Networked) networkBomb.RequestPullPin();
        else ApplyPinPulled();
    }

    // Runs on every headset. On the other player's side nobody holds the pin, so it just drops.
    public void ApplyPinPulled()
    {
        if (armed) return;
        armed = true;
        pinRequested = true;
        Debug.Log("Pin pulled, bomb armed");

        if (pin != null)
        {
            pin.transform.SetParent(null, true);
            StartCoroutine(DropPinWhenReleased());
        }
    }

    IEnumerator DropPinWhenReleased()
    {
        while (pin != null && pin.isSelected) yield return null;
        yield return null; // let XRI finish restoring the rigidbody first

        if (pin != null && pinRb != null)
        {
            pinRb.isKinematic = false;
            pinRb.useGravity = true;
            // Online the same pin goes back into the bomb on reset, so keep it.
            if (!Networked) Destroy(pin.gameObject, 8f);
        }
        pinReleased = true;
    }

    void Explode()
    {
        if (exploded) return;

        // Online: tell both players where it blew up. This headset (the thrower) puts it back afterwards.
        if (Networked)
        {
            resetHere = true;
            networkBomb.RequestExplode(transform.position);
        }
        else
        {
            ApplyExplosion(transform.position);
            StartCoroutine(RespawnRoutine());
        }
    }

    // Runs on every headset: push nearby objects, show the flash, hide the bomb.
    public void ApplyExplosion(Vector3 pos)
    {
        if (exploded) return;
        exploded = true;

        startPos = pos;
        startRot = transform.rotation;

        var pushed = new HashSet<Rigidbody>();
        foreach (var hit in Physics.OverlapSphere(pos, radius))
        {
            var body = hit.attachedRigidbody;
            if (body == null || body == rb || !pushed.Add(body)) continue;
            body.AddExplosionForce(force, pos, radius, upwardsModifier);
        }

        if (explosionPrefab != null)
            Instantiate(explosionPrefab, pos, Quaternion.identity);

        Debug.Log("Bomb exploded");

        // Hide this bomb but keep the object alive until the respawn is done.
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        grab.enabled = false;
        rb.isKinematic = true;

        if (resetHere)
        {
            resetHere = false;
            StartCoroutine(NetworkResetRoutine());
        }
    }

    // Online only, on the thrower's headset: move the same bomb back to the hand, then show it again everywhere.
    IEnumerator NetworkResetRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);
        while (!pinReleased) yield return null;

        Transform hand = ThrowingHand();
        networkBomb.Teleport(hand != null ? hand.position : startPos, hand != null ? hand.rotation : startRot);
        networkBomb.RequestReset();
        if (hand != null) HoverAt(hand);
    }

    // Runs on every headset: undo the explosion and put the pin back in.
    public void ApplyReset()
    {
        armed = fuseStarted = exploded = pinRequested = pinReleased = false;

        if (pin != null)
        {
            pin.transform.SetParent(transform, false);
            pin.transform.localPosition = pinRestLocalPos;
            pin.transform.localRotation = pinRestLocalRot;
            if (pinRb != null)
            {
                pinRb.isKinematic = pinStartKinematic;
                pinRb.useGravity = pinStartGravity;
            }
        }

        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = true;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = true;
        grab.enabled = true;
        rb.isKinematic = false;
    }

    Transform ThrowingHand()
    {
        var holderComp = lastHolder as Component;
        return holderComp != null ? lastHolder.GetAttachTransform(grab) : null;
    }

    IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);

        if (bombPrefab != null)
        {
            Transform hand = ThrowingHand();

            Vector3 spawnPos = hand != null ? hand.position : startPos;
            Quaternion spawnRot = hand != null ? hand.rotation : startRot;

            var clone = Instantiate(bombPrefab, spawnPos, spawnRot);
            if (clone.TryGetComponent<Bomb>(out var fresh))
            {
                fresh.bombPrefab = bombPrefab;
                fresh.explosionPrefab = explosionPrefab;
                if (hand != null) fresh.HoverAt(hand);
            }
        }

        // Stay alive until the player lets go of the pulled pin so its physics gets restored.
        while (!pinReleased) yield return null;
        Destroy(gameObject);
    }

    /// <summary>Holds the bomb at the hand (no gravity effect) until it is grabbed.</summary>
    public void HoverAt(Transform hand) => StartCoroutine(HoverRoutine(hand));

    IEnumerator HoverRoutine(Transform hand)
    {
        float t = 0f;
        while (t < hoverSeconds && hand != null && !grab.isSelected)
        {
            yield return new WaitForFixedUpdate();
            t += Time.fixedDeltaTime;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = hand.position;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
