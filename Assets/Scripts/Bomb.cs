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
/// Damage: the Prisoner within Kill Radius takes Close Damage (2 = dead), further out but within
/// Radius takes Far Damage (1 = injured). Respawn Delay is the bomb's cooldown.
/// Sounds: pin click, throw whoosh and explosion, heard by both players. Leave the clips empty to use
/// the placeholders from Tools > Escape Granny > Generate Weapon Sounds.
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
    [Tooltip("The Prisoner this close takes Close Damage; further out (up to Radius) takes Far Damage.")]
    [SerializeField] float killRadius = 1.5f;
    [SerializeField, Min(0)] int closeDamage = 2;
    [SerializeField, Min(0)] int farDamage = 1;
    [SerializeField] GameObject explosionPrefab;

    [Header("Sounds (empty = use Resources/WeaponSounds/BombPin, BombThrow, BombExplosion)")]
    [SerializeField] AudioClip pinSound;
    [SerializeField] AudioClip throwSound;
    [SerializeField] AudioClip explosionSound;
    [SerializeField, Range(0f, 1f)] float pinVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] float throwVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] float explosionVolume = 1f;
    [Tooltip("Pin and throw: can't be heard beyond this many metres.")]
    [SerializeField] float handSoundHearingDistance = 10f;
    [Tooltip("Explosion: can't be heard beyond this many metres. The house is 18 x 14 m.")]
    [SerializeField] float explosionHearingDistance = 40f;

    [Header("Respawn")]
    [SerializeField] GameObject bombPrefab;
    [SerializeField] float respawnDelay = 0.75f;
    [SerializeField] float hoverSeconds = 5f;

    XRGrabInteractable grab;
    Rigidbody rb;
    NetworkBomb networkBomb;
    AudioSource throwAudio;  // the whoosh in flight, kept so the explosion can cut it off
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

        // Placeholder sounds from Tools > Escape Granny > Generate Weapon Sounds, if none are dragged in.
        if (pinSound == null) pinSound = Resources.Load<AudioClip>("WeaponSounds/BombPin");
        if (throwSound == null) throwSound = Resources.Load<AudioClip>("WeaponSounds/BombThrow");
        if (explosionSound == null) explosionSound = Resources.Load<AudioClip>("WeaponSounds/BombExplosion");
        DropSound.Ensure(gameObject, "DropHeavy");

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

            // Online: tell both players, so the other side hears the throw too.
            if (Networked) networkBomb.RequestThrown();
            else ApplyThrown();
        }
    }

    // Runs on every headset: the whoosh of the bomb leaving the hand.
    public void ApplyThrown()
    {
        // Follows the bomb, so the whoosh flies with it. OneShotSound applies the distance and floor rules.
        throwAudio = OneShotSound.PlayAt(throwSound, transform.position, throwVolume, handSoundHearingDistance,
                                         1f, 0f, 1f, transform);
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
        OneShotSound.PlayAt(pinSound, transform.position, pinVolume, handSoundHearingDistance, 1f, 0f, 1f, transform);

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

        // Only the thrower's headset gets here, so the damage counts once.
        DealDamage(transform.position);

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

    void DealDamage(Vector3 pos)
    {
        foreach (var hit in Physics.OverlapSphere(pos, radius, ~0, QueryTriggerInteraction.Collide))
        {
            if (hit.GetComponent<PlayerHitbox>() == null) continue;
            float distance = Vector3.Distance(pos, hit.ClosestPoint(pos));
            int damage = distance <= killRadius ? closeDamage : farDamage;
            if (damage > 0) PlayerHitbox.TryHit(hit, damage);
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

        // On its own object, because this bomb is hidden now and (offline) destroyed soon after.
        if (throwAudio != null) Destroy(throwAudio.gameObject); // cut off a whoosh that's still playing
        OneShotSound.PlayAt(explosionSound, pos, explosionVolume, explosionHearingDistance, 3f, 0.05f);

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
