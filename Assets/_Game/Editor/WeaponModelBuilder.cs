// WeaponModelBuilder.cs
//
// Puts the real weapon prefabs (as small 3D models) on Granny's loadout buttons (AXE, KNIFE, GUN, BOMB).
//
// How to use:
//   Runs automatically as part of Tools > Escape Granny > Build Main Menu (MainMenuSceneBuilder).
//   Each weapon button gets a "Model" child: a linked copy of the weapon prefab, shrunk to fit the
//   button and floating just in front of it, with the button's label moved underneath. Because the
//   copies stay linked to their prefabs, changing a weapon prefab updates the menu too.
//   Colliders and Rigidbodies are removed from the copies so they don't fall or block the controller ray.
//   It replaces any old models (and removes the old picture icons, if any).
//
// To use a different model, change its path in Sources below. If a model sits at a bad angle,
// tweak ViewTilt (applied to every weapon) or that weapon's Spin, then rebuild the menu.

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class WeaponModelBuilder
{
    // Height of the strip at the bottom of each button kept for the text label (UI units).
    const float LabelStripHeight = 40f;

    // Gap around the model inside the button (UI units).
    const float Padding = 8f;

    // How far the model floats in front of the button (UI units), so the button doesn't cut through it.
    const float FrontOffset = 5f;

    // Model per weapon (the same ones used in Scenes/InteractablesSprintOne).
    // Spin rolls the model around the viewer's line of sight (degrees).
    static readonly (WeaponType weapon, string path, float spin)[] Sources =
    {
        (WeaponType.Axe, "Assets/6 Axe/Axe Realistic PBR/Built-In/Prefabs/Axe.prefab", 0f),
        (WeaponType.Knife, "Assets/1 Knife/KitchenRustyKnife/P_KitchenRustyKnife.prefab", 0f),
        (WeaponType.Gun, "Assets/4 Low Poly Guns/Models/Guns/pistol1/pistol1.fbx", 0f),
        (WeaponType.Bomb, "Assets/5 Stylized Explosives/Prefabs/Bomb3.prefab", 0f),
    };

    // Turns every model slightly so it reads as 3D instead of a flat side view.
    static readonly Vector3 ViewTilt = new Vector3(12f, -20f, 0f);

    // Adds a model to each weapon button of the loadout. Returns how many were added; missing
    // prefabs are skipped with a Console warning. The caller saves the scene.
    public static int AddModels(WeaponLoadout loadout)
    {
        var options = new SerializedObject(loadout).FindProperty("options");
        int added = 0;

        foreach (var (weapon, path, spin) in Sources)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[WeaponModelBuilder] Couldn't find the {weapon} prefab at {path}. Skipped it.");
                continue;
            }

            var button = FindButton(options, weapon);
            if (button == null)
            {
                Debug.LogWarning($"[WeaponModelBuilder] WeaponLoadout has no button for {weapon}. Skipped it.");
                continue;
            }

            RemoveChild(button.transform, "Icon");  // picture icon from the earlier version of this tool
            RemoveChild(button.transform, "Model"); // previous run
            PlaceModel(button, prefab, spin);
            MoveLabelToBottom(button);
            added++;
        }
        return added;
    }

    static Button FindButton(SerializedProperty options, WeaponType weapon)
    {
        for (int i = 0; i < options.arraySize; i++)
        {
            var element = options.GetArrayElementAtIndex(i);
            if (element.FindPropertyRelative("weapon").enumValueIndex == (int)weapon)
                return element.FindPropertyRelative("button").objectReferenceValue as Button;
        }
        return null;
    }

    static void RemoveChild(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child != null) Undo.DestroyObjectImmediate(child.gameObject);
    }

    static void PlaceModel(Button button, GameObject prefab, float spin)
    {
        var buttonRt = (RectTransform)button.transform;
        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, button.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(model, "Create weapon model");
        model.name = "Model";

        // For display only: nothing that falls, collides or catches the controller ray.
        foreach (var rb in model.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
        foreach (var col in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);

        // 1. Pose it at the world origin, unparented: longest side left-to-right, facing the viewer.
        model.transform.position = Vector3.zero;
        var prefabRotation = model.transform.rotation;
        var pose = Quaternion.Euler(0f, 0f, spin) * FaceViewer(model) * prefabRotation;
        model.transform.rotation = pose;

        // 2. Shrink it to fit the area above the label strip.
        var rect = buttonRt.rect;
        var area = new Vector2(rect.width - 2f * Padding, rect.height - LabelStripHeight - Padding);
        float uiToWorld = buttonRt.lossyScale.x;
        var bounds = GetBounds(model);
        float fit = Mathf.Min(area.x * uiToWorld / Mathf.Max(bounds.size.x, 0.0001f),
                              area.y * uiToWorld / Mathf.Max(bounds.size.y, 0.0001f));
        model.transform.localScale *= fit;
        bounds = GetBounds(model);

        // 3. Move it onto the button: centred in that area, just in front of the button's face
        //    (the viewer is on the button's -z side).
        float depth = bounds.size.z / uiToWorld;
        var target = new Vector3(rect.center.x, rect.center.y + (LabelStripHeight - Padding) / 2f,
                                 -(depth / 2f + FrontOffset));
        var centerOffset = bounds.center - model.transform.position;
        model.transform.rotation = buttonRt.rotation * pose;
        model.transform.position = buttonRt.TransformPoint(target) - buttonRt.rotation * centerOffset;
        model.transform.SetParent(buttonRt, true);
    }

    // Longest side left-to-right, thinnest side toward the viewer, then ViewTilt.
    static Quaternion FaceViewer(GameObject model)
    {
        var size = GetBounds(model).size;
        var axes = new[] { Vector3.right, Vector3.up, Vector3.forward };
        var lengths = new[] { size.x, size.y, size.z };
        System.Array.Sort(lengths, axes); // shortest first

        var thin = axes[0];
        var mid = axes[1];
        var alignToViewer = Quaternion.Inverse(Quaternion.LookRotation(thin, mid));
        return Quaternion.Euler(ViewTilt) * alignToViewer;
    }

    static Bounds GetBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.1f);

        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        return bounds;
    }

    static void MoveLabelToBottom(Button button)
    {
        var label = button.transform.Find("Label");
        if (label == null) return;

        var labelRt = (RectTransform)label;
        Undo.RecordObject(labelRt, "Move weapon label");
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = new Vector2(1f, 0f);
        labelRt.pivot = new Vector2(0.5f, 0f);
        labelRt.anchoredPosition = new Vector2(0f, 2f);
        labelRt.sizeDelta = new Vector2(0f, LabelStripHeight);

        var labelText = label.GetComponent<Text>();
        Undo.RecordObject(labelText, "Align weapon label");
        labelText.alignment = TextAnchor.LowerCenter; // big fonts grow upward, not off the button
    }
}
