using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Put in Assets/Editor. Select your Room (or Foundation), then use
/// Tools > Trim Environment Far From Selection.
/// Disables or deletes trees, bushes, rocks, cliffs, etc. farther than Radius meters (sideways) from the selection.
/// Undo with Cmd+Z.
/// </summary>
public static class TrimEnvironment
{
    const float Radius = 45f;

    // Groups whose direct children get trimmed. Add or remove names to taste.
    static readonly string[] Groups =
    {
        "Trees", "Bushes", "Rocks", "Cliffs", "Waterplants", "ButterflySpawnAreas"
    };

    [MenuItem("Tools/Trim Environment Far From Selection")]
    static void Trim()
    {
        Transform center = Selection.activeTransform;
        if (center == null)
        {
            EditorUtility.DisplayDialog("Trim Environment", "Select your Room or Foundation in the Hierarchy first.", "OK");
            return;
        }

        var toTrim = new List<GameObject>();
        int kept = 0;

        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (System.Array.IndexOf(Groups, t.name) < 0) continue;
            foreach (Transform child in t)
            {
                Vector3 d = child.position - center.position;
                d.y = 0f;
                if (d.magnitude > Radius && child.gameObject.activeSelf) toTrim.Add(child.gameObject);
                else kept++;
            }
        }

        if (toTrim.Count == 0)
        {
            EditorUtility.DisplayDialog("Trim Environment", $"Nothing farther than {Radius} m from {center.name}.", "OK");
            return;
        }

        int choice = EditorUtility.DisplayDialogComplex("Trim Environment",
            $"{toTrim.Count} objects are farther than {Radius} m from {center.name} ({kept} stay).\n\n" +
            "Disable = hidden but can be turned back on. Delete = removed from the scene.",
            "Disable", "Cancel", "Delete");

        if (choice == 1) return;

        Undo.SetCurrentGroupName("Trim environment");
        int group = Undo.GetCurrentGroup();
        foreach (var go in toTrim)
        {
            if (choice == 0) { Undo.RecordObject(go, "Disable"); go.SetActive(false); }
            else Undo.DestroyObjectImmediate(go);
        }
        Undo.CollapseUndoOperations(group);

        Debug.Log($"{(choice == 0 ? "Disabled" : "Deleted")} {toTrim.Count} objects beyond {Radius} m of {center.name}.");
    }
}
