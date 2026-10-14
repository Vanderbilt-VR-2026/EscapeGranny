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
    IXRSelectInteractor lastHolder;
    Vector3 pinRestLocalPos;
    Vector3 startPos;
    Quaternion startRot;
    bool armed, fuseStarted, exploded, pinReleased;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();

        if (pin != null)
        {
            pinRestLocalPos = pin.transform.localPosition;

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
        if (armed || pin == null || !pin.isSelected) return;

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
        if (armed) return;
        armed = true;
        Debug.Log("Pin pulled, bomb armed");

        if (pin != null)
        {
            pin.transform.SetParent(null, true);
            StartCoroutine(DropPinWhenReleased());
        }
    }

    IEnumerator DropPinWhenReleased()
    {
        var pinRb = pin.GetComponent<Rigidbody>();

        while (pin != null && pin.isSelected) yield return null;
        yield return null; // let XRI finish restoring the rigidbody first

        if (pin != null && pinRb != null)
        {
            pinRb.isKinematic = false;
            pinRb.useGravity = true;
            Destroy(pin.gameObject, 8f);
        }
        pinReleased = true;
    }

    void Explode()
    {
        if (exploded) return;
        exploded = true;

        Vector3 pos = transform.position;
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

        StartCoroutine(RespawnRoutine());
    }

    IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);

        if (bombPrefab != null)
        {
            Transform hand = null;
            var holderComp = lastHolder as Component;
            if (holderComp != null) hand = lastHolder.GetAttachTransform(grab);

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
