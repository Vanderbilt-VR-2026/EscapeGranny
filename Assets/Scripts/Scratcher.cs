using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Put on Knife_Root and Axe_Root (needs a Rigidbody).
/// Every physics step, each blade collider checks what surface it is touching (table, floor,
/// other objects). Dragging draws ONE smooth line per stroke (a LineRenderer); a fast hit leaves
/// a short gash. This does not rely on OnCollision callbacks, so it still works when a bigger
/// handle collider is what physically stops the object.
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

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        var list = new List<Collider>();
        if (bladeColliders != null)
            foreach (var c in bladeColliders)
                if (c != null) list.Add(c);

        if (list.Count == 0)
            foreach (var c in GetComponentsInChildren<Collider>())
                if (!c.isTrigger) list.Add(c);

        blades = list.ToArray();
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

        wasTouching = touching;
        previousVelocity = rb.linearVelocity;
    }

    void Update()
    {
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