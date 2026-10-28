using System.Collections;
using System.Collections.Generic;
using UnityEngine; 

/// <summary>
/// Builds a set of chains across the door and drops them all (one after another) when ReleaseAll() is called.
/// Put on an empty object that is a child of Room, placed on the front face of the door wall
/// (Room-local position about 0, 0, 1.92). Everything is measured in this object's own space:
/// X = across the door, Y = height above the floor, -Z = toward the player.
/// EscapeDoor calls ReleaseAll() when the padlock opens.
/// </summary>
public class ChainPattern : MonoBehaviour
{
    [Header("Layout")]
    [Tooltip("Chains run from -this to +this across the door (a bit wider than the door opening so they reach the wall).")]
    [SerializeField] float halfSpan = 0.72f;
    [SerializeField] float bottom = 0.4f;
    [SerializeField] float top = 1.9f;
    [Tooltip("Horizontal chains across the door.")]
    [SerializeField, Range(0, 8)] int rows = 3;
    [Tooltip("Two chains crossing in an X.")]
    [SerializeField] bool crossDiagonals = true;

    [Header("Look")]
    [SerializeField] float sag = 0.04f;
    [Tooltip("Longer = fewer links = better performance on Quest.")]
    [SerializeField] float linkLength = 0.08f;
    [SerializeField] float thickness = 0.012f;
    [SerializeField] Material linkMaterial;
    [Tooltip("Gap between chains toward the player so they don't overlap.")]
    [SerializeField] float depthStep = 0.012f;

    [Header("Release")]
    [Tooltip("Seconds between each chain dropping.")]
    [SerializeField] float releaseStagger = 0.08f;

    readonly List<ChainBuilder> chains = new List<ChainBuilder>();
    int made;
    bool released;

    void Start() => Create();

    [ContextMenu("Rebuild")]
    public void Create()
    {
        foreach (var c in chains) if (c != null) Destroy(c.gameObject);
        chains.Clear();
        made = 0;
        released = false;

        for (int i = 0; i < rows; i++)
        {
            float t = rows == 1 ? 0.5f : i / (float)(rows - 1);
            float y = Mathf.Lerp(bottom, top, t);
            AddChain(new Vector3(-halfSpan, y, 0f), new Vector3(halfSpan, y, 0f));
        }

        if (crossDiagonals)
        {
            AddChain(new Vector3(-halfSpan, bottom, 0f), new Vector3(halfSpan, top, 0f));
            AddChain(new Vector3(halfSpan, bottom, 0f), new Vector3(-halfSpan, top, 0f));
        }
    }

    void AddChain(Vector3 localA, Vector3 localB)
    {
        float z = -made * depthStep;
        localA.z = z; localB.z = z;
        made++;

        var go = new GameObject("DoorChain" + made);
        go.transform.SetParent(transform, false);

        var a = new GameObject("Start").transform;
        a.SetParent(go.transform, false);
        a.localPosition = localA;
        var b = new GameObject("End").transform;
        b.SetParent(go.transform, false);
        b.localPosition = localB;

        float length = Vector3.Distance(transform.TransformPoint(localA), transform.TransformPoint(localB));
        int links = Mathf.Max(3, Mathf.CeilToInt(length / Mathf.Max(0.02f, linkLength)));

        var chain = go.AddComponent<ChainBuilder>();
        chain.Setup(a, b, links, sag, thickness, linkMaterial);
        chains.Add(chain);
    }

    [ContextMenu("Release All")]
    public void ReleaseAll()
    {
        if (released) return;
        released = true;
        StartCoroutine(ReleaseRoutine());
    }

    IEnumerator ReleaseRoutine()
    {
        foreach (var c in chains)
        {
            if (c != null) c.Release();
            if (releaseStagger > 0f) yield return new WaitForSeconds(releaseStagger);
        }
    }
}
