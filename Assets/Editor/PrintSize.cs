using UnityEngine;
using UnityEditor;

public static class PrintSize
{
    [MenuItem("Tools/Print Selected Size")]
    static void Print()
    {
        foreach (var go in Selection.gameObjects)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) continue;
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            Debug.Log($"{go.name}: {b.size.x:F3} x {b.size.y:F3} x {b.size.z:F3} m (longest {Mathf.Max(b.size.x, b.size.y, b.size.z):F3})");
        }
    }
}