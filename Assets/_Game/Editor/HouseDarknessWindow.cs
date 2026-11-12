// HouseDarknessWindow.cs
//
// Makes the house darker (or brighter) in one place. Most of the light inside comes from the BAKED
// lightmaps (the room lamps and the ambient light), so changing fog or the moon barely shows; the
// lamps and ambient have to change and then the lighting has to be baked again.
//
// How to use: open House_Bishal, then Tools > Escape Granny > House Darkness...
//   - Lamp brightness: scales every lamp inside the house by the same factor (0.5 = half as bright),
//     keeping the differences between rooms. Only lights under "House (generated)" and
//     "House Lighting (generated)": not the outdoor sun or the escape-door lights.
//   - Ambient light: the base light everywhere, even where no lamp reaches. Near black = darker.
//   - Generate Lighting: rebakes the lightmaps so the changes show (takes a few minutes).
// Lamp changes can be undone with Cmd+Z; to undo the ambient color, just set it again (the window
// starts with the scene's current color). Save the scene afterwards.

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class HouseDarknessWindow : EditorWindow
{
    static readonly string[] HouseRoots = { "House (generated)", "House Lighting (generated)" };

    float lampFactor = 0.5f;
    Color ambient = new Color(0.04f, 0.04f, 0.05f);

    [MenuItem("Tools/Escape Granny/House Darkness...")]
    static void Open()
    {
        var window = GetWindow<HouseDarknessWindow>("House Darkness");
        window.ambient = RenderSettings.ambientLight;
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox("The light inside the house is mostly baked. Change the lamps and ambient " +
                                "light here, then press Generate Lighting so it shows in the game.", MessageType.Info);
        EditorGUILayout.LabelField("Open scene", EditorSceneManager.GetActiveScene().name);
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Lamps", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("House lamps now", DescribeLamps());
        lampFactor = EditorGUILayout.Slider(new GUIContent("Multiply brightness by",
            "0.5 = half as bright, 2 = twice as bright. Applied each time you press the button."), lampFactor, 0.1f, 2f);
        if (GUILayout.Button($"Apply to house lamps (x{lampFactor:0.##})")) ScaleLamps(lampFactor);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Ambient light", EditorStyles.boldLabel);
        ambient = EditorGUILayout.ColorField(new GUIContent("Ambient color",
            "Base light everywhere. Near black (like 0A0A0D) is very dark."), ambient);
        if (GUILayout.Button("Apply ambient light")) ApplyAmbient();

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(Lightmapping.isRunning))
            if (GUILayout.Button(Lightmapping.isRunning ? "Baking..." : "Generate Lighting (rebake)", GUILayout.Height(30)))
                Lightmapping.BakeAsync();
        if (Lightmapping.isRunning) EditorGUILayout.HelpBox("Baking. You can keep working; save the scene when it finishes.", MessageType.None);
    }

    void OnInspectorUpdate() => Repaint(); // keep the lamp summary and bake status current

    static Light[] HouseLamps()
    {
        var lamps = new System.Collections.Generic.List<Light>();
        foreach (string rootName in HouseRoots)
        {
            var root = GameObject.Find(rootName);
            if (root == null) continue;
            foreach (var light in root.GetComponentsInChildren<Light>(true))
                if (light.type != LightType.Directional) lamps.Add(light);
        }
        return lamps.ToArray();
    }

    static string DescribeLamps()
    {
        var lamps = HouseLamps();
        if (lamps.Length == 0) return "none found (open House_Bishal)";
        float min = float.MaxValue, max = 0f;
        foreach (var l in lamps) { min = Mathf.Min(min, l.intensity); max = Mathf.Max(max, l.intensity); }
        return $"{lamps.Length} lamps, intensity {min:0.##} to {max:0.##}";
    }

    static void ScaleLamps(float factor)
    {
        var lamps = HouseLamps();
        if (lamps.Length == 0) return;
        Undo.RecordObjects(lamps, "Scale House Lamps");
        foreach (var l in lamps)
        {
            l.intensity *= factor;
            // FlickerLight sets its own brightness between min and max; scale those too so flickering lamps match.
            var flicker = l.GetComponent<FlickerLight>();
            if (flicker != null)
            {
                Undo.RecordObject(flicker, "Scale House Lamps");
                flicker.minIntensity *= factor;
                flicker.maxIntensity *= factor;
            }
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[HouseDarkness] Scaled {lamps.Length} house lamps by x{factor:0.##}. Press Generate Lighting to see it in the game.");
    }

    void ApplyAmbient()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = ambient;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[HouseDarkness] Ambient light set to #{ColorUtility.ToHtmlStringRGB(ambient)}. Press Generate Lighting to see it in the game.");
    }
}
