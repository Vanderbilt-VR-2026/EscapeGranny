using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Put on Knife_Root and Axe_Root (needs a Rigidbody).
/// Every physics step, each blade collider checks what surface it is touching (table, floor,
/// other objects). Dragging draws ONE smooth line per stroke (a LineRenderer); a fast hit leaves
/// a short gash. This does not rely on OnCollision callbacks, so it still works when a bigger
/// handle collider is what physically stops the object.
/// Sounds: a slash when the moving blade hits something new (wall, furniture, floor), and a different
/// one when it hits the other player. Each headset detects hits itself, so both players hear them.
/// Leave the clips empty to use the placeholders from Tools > Escape Granny > Generate Weapon Sounds.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Scratcher : MonoBehaviour
{
    [Tooltip("Unlit material that sets the scratch color (for example ScratchRed). Set its Render Face to Both.")]
    [SerializeField] Material scratchMaterial;

    [Tooltip("Only these colliders count as the blade. Leave empty to use all colliders on this object.")]
    [SerializeField] Collider[] bladeColliders;

    [Tooltip("How close (meters) the blade must be to a surface to count as touching it.")]
    [SerializeField] float touchDistance = 0.012f;
    [Tooltip("Which layers can be scratched.")]
    [SerializeField] LayerMask scratchableLayers = ~0;

    [SerializeField] float width = 0.004f;
    [Tooltip("Distance the blade must travel before a new point is added to the line.")]
    [SerializeField] float pointSpacing = 0.015f;
    [Tooltip("Lower = smoother but the line lags the blade a little. 1 = no smoothing.")]
    [SerializeField, Range(0.05f, 1f)] float smoothing = 0.3f;
    [SerializeField] float surfaceOffset = 0.002f;
    [Tooltip("Contact can flicker; the stroke stays open for this many seconds after contact is lost.")]
    [SerializeField] float gapTolerance = 0.12f;
    [Tooltip("A contact point jumping further than this ends the stroke and starts a new one.")]
    [SerializeField] float maxJump = 0.2f;
    [SerializeField] float chopSpeed = 2f;
    [SerializeField] float chopLength = 0.04f;
    [SerializeField] int maxStrokes = 60;

    [Header("Hit sounds (empty = use Resources/WeaponSounds/BladeHitSurface, BladeHitPlayer)")]
    [SerializeField] AudioClip surfaceHitSound;
    [SerializeField] AudioClip playerHitSound;
    [Tooltip("The blade must be moving at least this fast (m/s) to make a hit sound, so resting it on a table is quiet.")]
    [SerializeField] float minHitSpeed = 0.8f;
    [Tooltip("Hits at this speed (m/s) or faster are full volume.")]
    [SerializeField] float fullVolumeHitSpeed = 4f;
    [SerializeField, Range(0f, 1f)] float hitVolume = 0.9f;
    [Tooltip("Shortest time (s) between two hit sounds, so a jittery contact doesn't rattle.")]
    [SerializeField] float hitSoundCooldown = 0.15f;
    [Tooltip("1 = as recorded. Try about 0.8 on the axe for a heavier, deeper sound.")]
    [SerializeField, Range(0.5f, 1.5f)] float hitPitch = 1f;
    [SerializeField] float hitHearingDistance = 15f;

    [Tooltip("Logs what the blade starts touching. Turn off once scratching works.")]
    [SerializeField] bool debugContacts = false;

    readonly Queue<GameObject> finished = new Queue<GameObject>();
    readonly Collider[] overlapBuffer = new Collider[16];

    Rigidbody rb;
    Collider[] blades;
    LineRenderer current;
    Vector3 smoothed, lastAdded, strokeNormal;
    Vector3 previousVelocity;
    Collider lastTouched;
    float lastContactTime;
    bool wasTouching;

    XRGrabInteractable grab;
    NetworkObject networkObject;
    Vector3 lastBladeCenter;
    float bladeSpeed;          // measured from movement, so it also works on the other player's copy
    Collider lastHitCollider;  // a new sound plays only when the blade touches something new
    float lastHitSoundTime = -999f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
        networkObject = GetComponent<NetworkObject>();

        // Placeholders from Tools > Escape Granny > Generate Weapon Sounds, if none are dragged in.
        if (surfaceHitSound == null) surfaceHitSound = Resources.Load<AudioClip>("WeaponSounds/BladeHitSurface");
        if (playerHitSound == null) playerHitSound = Resources.Load<AudioClip>("WeaponSounds/BladeHitPlayer");
        DropSound.Ensure(gameObject, "DropHeavy"); // clank when the knife/axe lands on the floor

        var list = new List<Collider>();
        if (bladeColliders != null)
            foreach (var c in bladeColliders)
                if (c != null) list.Add(c);

        if (list.Count == 0)
            foreach (var c in GetComponentsInChildren<Collider>())
                if (!c.isTrigger) list.Add(c);

        blades = list.ToArray();
        lastBladeCenter = BladeCenter();
    }

    void FixedUpdate()
    {
        bool touching = FindContact(out Vector3 p, out Vector3 n, out Collider other);

        if (touching)
        {
            if (debugContacts && (!wasTouching || other != lastTouched))
                Debug.Log($"{name}: blade touching {other.name}", this);

            if (!wasTouching)
                TryChop(p, n, other);

            ContinueStroke(p, n);
            lastContactTime = Time.time;
            lastTouched = other;
        }

        UpdateHitSound(touching, p, other);

        wasTouching = touching;
        previousVelocity = rb.linearVelocity;
    }

    void Update()
    {
        // Blade speed from how far it moved this frame. Rigidbody velocity would be 0 on the other
        // player's headset, where Fusion moves the weapon directly. Keep the peak for a moment, since
        // the network moves it in small jumps.
        Vector3 center = BladeCenter();
        float frameSpeed = Time.deltaTime > 0f ? Vector3.Distance(center, lastBladeCenter) / Time.deltaTime : 0f;
        lastBladeCenter = center;
        bladeSpeed = Mathf.Max(frameSpeed, Mathf.MoveTowards(bladeSpeed, 0f, 20f * Time.deltaTime));

        if (current != null && Time.time - lastContactTime > gapTolerance)
            EndStroke();
    }

    void OnDisable() => EndStroke();

    // ---------- contact detection ----------

    bool FindContact(out Vector3 point, out Vector3 normal, out Collider touched)
    {
        point = normal = Vector3.zero;
        touched = null;
        float bestGap = float.MaxValue;

        foreach (var blade in blades)
        {
            if (blade == null || !blade.enabled) continue;

            Bounds b = blade.bounds;
            int count = Physics.OverlapBoxNonAlloc(
                b.center, b.extents + Vector3.one * touchDistance, overlapBuffer,
                Quaternion.identity, scratchableLayers, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider other = overlapBuffer[i];
                if (other == null || other.isTrigger) continue;
                if (other.attachedRigidbody == rb) continue;   // ignore our own colliders

                if (!MeasureContact(blade, other, out Vector3 p, out Vector3 n, out float gap)) continue;
                if (gap < bestGap)
                {
                    bestGap = gap;
                    point = p;
                    normal = n;
                    touched = other;
                }
            }
        }

        return touched != null;
    }

    bool MeasureContact(Collider blade, Collider other, out Vector3 point, out Vector3 normal, out float gap)
    {
        point = normal = Vector3.zero;
        gap = 0f;

        // Overlapping: use the separation direction (points out of the other surface).
        if (Physics.ComputePenetration(
                blade, blade.transform.position, blade.transform.rotation,
                other, other.transform.position, other.transform.rotation,
                out Vector3 dir, out float depth))
        {
            Vector3 bladeFace = blade.ClosestPoint(blade.bounds.center - dir * 10f);
            point = bladeFace + dir * depth;
            normal = dir;
            gap = -depth;
            return true;
        }

        // Not overlapping: check how close the blade is.
        if (other is MeshCollider mc && !mc.convex) return false;   // ClosestPoint can't handle these

        Vector3 center = blade.bounds.center;
        Vector3 onOther = other.ClosestPoint(center);
        if ((onOther - center).sqrMagnitude < 1e-10f) return false;

        Vector3 onBlade = blade.ClosestPoint(onOther);
        gap = Vector3.Distance(onOther, onBlade);
        if (gap > touchDistance) return false;

        normal = gap > 1e-5f ? (onBlade - onOther) / gap : (center - onOther).normalized;
        point = onOther;

        // Refine to the true surface point and normal.
        if (other.Raycast(new Ray(onOther + normal * 0.02f, -normal), out RaycastHit hit, 0.05f))
        {
            point = hit.point;
            normal = hit.normal;
        }
        return true;
    }

    // ---------- hit sounds ----------
    // Each headset checks for itself, so both players hear the hit without any networking.

    void UpdateHitSound(bool touchingSurface, Vector3 surfacePoint, Collider surface)
    {
        // A player counts first, so slashing someone standing against a wall sounds like hitting them.
        bool hitPlayer = FindPlayerContact(out Collider hit, out Vector3 point);
        // Any player's body (including the holder's own) is never treated as a wall.
        if (!hitPlayer && touchingSurface && !IsAnyPlayer(surface))
        {
            hit = surface;
            point = surfacePoint;
        }

        if (hit != null && hit != lastHitCollider
            && bladeSpeed >= minHitSpeed && Time.time - lastHitSoundTime >= hitSoundCooldown)
        {
            lastHitSoundTime = Time.time;
            float volume = hitVolume * Mathf.Lerp(0.4f, 1f, Mathf.InverseLerp(minHitSpeed, fullVolumeHitSpeed, bladeSpeed));
            OneShotSound.PlayAt(hitPlayer ? playerHitSound : surfaceHitSound, point, volume,
                                hitHearingDistance, 1.5f, 0.1f, hitPitch);
        }
        lastHitCollider = hit;
    }

    // The player's body colliders are triggers, which the scratch check skips, so look for them separately.
    bool FindPlayerContact(out Collider touched, out Vector3 point)
    {
        touched = null;
        point = Vector3.zero;
        foreach (var blade in blades)
        {
            if (blade == null || !blade.enabled) continue;
            Bounds b = blade.bounds;
            int count = Physics.OverlapBoxNonAlloc(b.center, b.extents + Vector3.one * touchDistance, overlapBuffer,
                                                   Quaternion.identity, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider other = overlapBuffer[i];
                if (other == null || other.attachedRigidbody == rb || !IsPlayerBody(other)) continue;
                touched = other;
                point = other.ClosestPoint(b.center);
                return true;
            }
        }
        return false;
    }

    // True for the body of the player being attacked, never the one holding this weapon.
    bool IsPlayerBody(Collider c)
    {
        if (c == null) return false;
        bool heldHere = grab != null && grab.isSelected; // only the holder's headset sees it as selected

        // Network avatars (online). Each player's avatar is "owned" (state authority) by that player.
        var avatar = c.GetComponentInParent<FusionNetworkPlayer>();
        if (avatar != null)
        {
            if (avatar.Object == null || !avatar.Object.IsValid) return false;
            if (heldHere) return !avatar.HasStateAuthority; // anyone but me
            // Held on the other headset: grabbing gives the holder authority over the weapon, so skip their avatar.
            return networkObject == null || !networkObject.IsValid
                || avatar.Object.StateAuthority != networkObject.StateAuthority;
        }

        // This headset's own body. It's being hit only if someone else is holding the weapon.
        if (c is CharacterController) return !heldHere;
        return false;
    }

    static bool IsAnyPlayer(Collider c) =>
        c != null && (c is CharacterController || c.GetComponentInParent<FusionNetworkPlayer>() != null);

    Vector3 BladeCenter()
    {
        if (blades == null || blades.Length == 0) return transform.position;
        Vector3 sum = Vector3.zero;
        int n = 0;
        foreach (var blade in blades)
            if (blade != null) { sum += blade.bounds.center; n++; }
        return n > 0 ? sum / n : transform.position;
    }

    // ---------- strokes ----------

    void TryChop(Vector3 p, Vector3 n, Collider other)
    {
        Vector3 vel = previousVelocity;
        if (other.attachedRigidbody != null) vel -= other.attachedRigidbody.linearVelocity;
        if (vel.magnitude < chopSpeed) return;

        Vector3 dir = Vector3.ProjectOnPlane(vel, n);
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.ProjectOnPlane(transform.forward, n);
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();

        EndStroke();
        StartStroke(p - dir * (chopLength * 0.5f), n);
        AddPoint(p + dir * (chopLength * 0.5f));
        EndStroke();
    }

    void ContinueStroke(Vector3 p, Vector3 n)
    {
        if (current == null)
        {
            StartStroke(p, n);
            return;
        }

        if (Vector3.Distance(p, smoothed) > maxJump)
        {
            EndStroke();
            StartStroke(p, n);
            return;
        }

        smoothed = Vector3.Lerp(smoothed, p, smoothing);
        if (Vector3.Distance(smoothed, lastAdded) >= pointSpacing)
            AddPoint(smoothed);
    }

    void StartStroke(Vector3 p, Vector3 n)
    {
        if (scratchMaterial == null)
        {
            Debug.LogWarning("Scratcher: assign a Scratch Material.", this);
            return;
        }

        var go = new GameObject("Scratch");
        go.transform.rotation = Quaternion.LookRotation(n);

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.alignment = LineAlignment.TransformZ;   // line lies flat on the surface
        lr.widthMultiplier = width;
        lr.numCapVertices = 2;
        lr.numCornerVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.sharedMaterial = scratchMaterial;
        lr.positionCount = 0;

        current = lr;
        strokeNormal = n;
        smoothed = p;
        AddPoint(p);
    }

    void AddPoint(Vector3 p)
    {
        if (current == null) return;
        current.positionCount++;
        current.SetPosition(current.positionCount - 1, p + strokeNormal * surfaceOffset);
        lastAdded = p;
    }

    void EndStroke()
    {
        if (current == null) return;

        GameObject go = current.gameObject;
        if (current.positionCount < 2)
        {
            Destroy(go);
        }
        else
        {
            finished.Enqueue(go);
            while (finished.Count > maxStrokes)
            {
                GameObject old = finished.Dequeue();
                if (old != null) Destroy(old);
            }
        }
        current = null;
    }

    // Shows the blade colliders (red) in the Scene view when this object is selected.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        if (bladeColliders == null) return;
        foreach (var c in bladeColliders)
            if (c != null) Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }
}