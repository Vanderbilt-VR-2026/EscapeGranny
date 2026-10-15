using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a hanging chain between two points (no chain asset needed) and can drop it.
/// Put on an empty "Chain" object. Assign Start (door handle) and End (door frame).
/// Call Release() (EscapeDoor does this) and every link falls to the floor.
/// A HangingPadlock can pull the middle of the chain with it (SetPull) so the shackle stays threaded.
/// </summary>
public class ChainBuilder : MonoBehaviour
{
    [SerializeField] Transform start;
    [SerializeField] Transform end;
    [SerializeField] int links = 9;
    [Tooltip("How far the middle of the chain hangs below the straight line (meters).")]
    [SerializeField] float sag = 0.12f;
    [SerializeField] float thickness = 0.012f;
    [SerializeField] Material linkMaterial;
    [SerializeField] float linkMass = 0.15f;

    readonly List<GameObject> linkObjects = new List<GameObject>();
    bool released;

    // Pull state (set by HangingPadlock)
    float pullT;
    Vector3 pullOffset;
    float pullWidth = 0.3f;

    public bool IsReleased => released;

    void Start() => Build();

    [ContextMenu("Rebuild")]
    public void Build()
    {
        if (start == null || end == null) { Debug.LogWarning("ChainBuilder: assign Start and End."); return; }

        foreach (var l in linkObjects) if (l != null) Destroy(l);
        linkObjects.Clear();
        released = false;
        pullOffset = Vector3.zero;

        for (int i = 0; i < links; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "ChainLink" + i;
            go.transform.SetParent(transform, true);

            var col = go.GetComponent<Collider>();
            col.enabled = false;                       // doesn't block the player while hanging
            if (linkMaterial != null) go.GetComponent<Renderer>().sharedMaterial = linkMaterial;
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            linkObjects.Add(go);
            PoseLink(i);
        }
    }

    // Puts link i between its two points on the (possibly pulled) curve.
    void PoseLink(int i)
    {
        if (i >= linkObjects.Count || linkObjects[i] == null) return;

        Vector3 a = PointOnCurve(i / (float)links);
        Vector3 b = PointOnCurve((i + 1) / (float)links);
        Vector3 dir = b - a;
        float len = dir.magnitude;
        if (len < 0.0001f) return;

        var t = linkObjects[i].transform;
        t.position = (a + b) * 0.5f;
        // Capsule is 2 tall on its Y axis, so Y scale = length / 2. Overlap links slightly.
        t.rotation = Quaternion.FromToRotation(Vector3.up, dir) *
                     Quaternion.Euler(0f, (i % 2) * 90f, 0f);
        float flat = (i % 2 == 0) ? thickness * 1.8f : thickness * 0.8f;
        t.localScale = new Vector3(flat, len * 0.62f, thickness * (flat > thickness ? 0.8f : 1.8f));
    }

    /// <summary>World position along the hanging chain. 0 = start, 1 = end, 0.5 = the middle.</summary>
    public Vector3 PointAt(float t)
    {
        if (start == null || end == null) return transform.position;
        return BaseCurve(Mathf.Clamp01(t));
    }

    /// <summary>
    /// Drags the chain around point t by "offset" (meters), smoothly fading out along the chain.
    /// Use Vector3.zero to put it back. Does nothing once the chain has been released.
    /// </summary>
    public void SetPull(float t, Vector3 offset, float width = 0.3f)
    {
        if (released || start == null || end == null) return;
        if (offset.sqrMagnitude < 1e-8f && pullOffset.sqrMagnitude < 1e-8f) return;

        pullT = t;
        pullOffset = offset;
        pullWidth = Mathf.Max(0.05f, width);
        for (int i = 0; i < linkObjects.Count; i++) PoseLink(i);
    }

    Vector3 BaseCurve(float t)
    {
        Vector3 p = Vector3.Lerp(start.position, end.position, t);
        p += Vector3.down * (sag * 4f * t * (1f - t));   // parabola, deepest at t = 0.5
        return p;
    }

    Vector3 PointOnCurve(float t)
    {
        Vector3 p = BaseCurve(t);
        if (pullOffset.sqrMagnitude > 1e-8f)
        {
            float x = Mathf.Abs(t - pullT) / pullWidth;
            if (x < 1f)
            {
                float w = 1f - x;
                p += pullOffset * (w * w * (3f - 2f * w));   // smooth falloff
            }
        }
        return p;
    }

    [ContextMenu("Release")]
    public void Release()
    {
        if (released) return;
        released = true;

        var cols = new List<Collider>();
        foreach (var l in linkObjects)
        {
            if (l == null) continue;
            var col = l.GetComponent<Collider>();
            col.enabled = true;
            cols.Add(col);

            var rb = l.AddComponent<Rigidbody>();
            rb.mass = linkMass;
            rb.AddForce(new Vector3(Random.Range(-0.3f, 0.3f), 0.1f, Random.Range(-0.3f, 0.3f)), ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 0.2f, ForceMode.Impulse);
        }

        // Links overlap each other, so stop them colliding or they will explode apart.
        for (int i = 0; i < cols.Count; i++)
            for (int j = i + 1; j < cols.Count; j++)
                Physics.IgnoreCollision(cols[i], cols[j]);
    }
}