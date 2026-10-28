
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class LampPlacer
{
    const string LampPrefabPath = "Assets/New Solution Studio/PBR Lamps Pack/Prefabs/Small_roof_lamp.prefab";
    const string HouseScenePath = "Assets/House_Bishal.unity";
    const string LightingRootName = "House Lighting (generated)";
    const string LampRootName = "House Lamps (generated)";
    const float LightHeightAboveFloor = 2.5f; // HouseBuilder puts room lights 2.5 m above the floor
    const float CeilingHeight = 2.8f;          // ceiling is 2.8 m above the floor

    // The model was exported lying on its side, so tilt it to hang straight down.
    // (-90 made the light face up, so it's +90.)
    static readonly Vector3 LampRotation = new Vector3(90f, 0f, 0f);

    [MenuItem("Tools/Escape Granny/Add Ceiling Lamps")]
    static void AddCeilingLamps()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LampPrefabPath);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Add Ceiling Lamps", "Couldn't find the lamp at:\n" + LampPrefabPath, "OK");
            return;
        }

        // Get out of prefab editing and make sure the house scene is the one open.
        StageUtility.GoToMainStage();
        if (SceneManager.GetActiveScene().path != HouseScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(HouseScenePath);
        }

        FixMaterialsForURP(prefab);

        // The lamps go where the room lights are; create the lights first if they're missing.
        var lightingRoot = GameObject.Find(LightingRootName);
        if (lightingRoot == null)
        {
            EditorApplication.ExecuteMenuItem("Tools/Escape Granny/Apply Horror Lighting");
            lightingRoot = GameObject.Find(LightingRootName);
        }
        if (lightingRoot == null)
        {
            EditorUtility.DisplayDialog("Add Ceiling Lamps", "No room lights found. Run Tools > Escape Granny > Apply Horror Lighting, then try again.", "OK");
            return;
        }

        // Replace any lamps from a previous run.
        var old = GameObject.Find(LampRootName);
        if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject(LampRootName);
        Undo.RegisterCreatedObjectUndo(root, "Add Ceiling Lamps");

        int count = 0;
        foreach (var light in lightingRoot.GetComponentsInChildren<Light>(true))
        {
            if (light.type != LightType.Point) continue; // skip the moonlight

            Vector3 p = light.transform.position;
            float ceilingY = p.y - LightHeightAboveFloor + CeilingHeight;

            var lamp = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            // The lights are already called "Lamp - <room>", so just say it's the ceiling model.
            lamp.name = light.gameObject.name.Replace("Lamp - ", "Ceiling Lamp - ");
            lamp.transform.position = new Vector3(p.x, ceilingY, p.z);
            lamp.transform.rotation = Quaternion.Euler(LampRotation);

            // Slide the model so its top just touches the ceiling (done after rotating).
            var renderers = lamp.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                lamp.transform.position += Vector3.up * (ceilingY - 0.01f - b.max.y);
            }

            // No shadows (too expensive on Quest) and no extra lights (the room light already exists).
            foreach (var r in renderers) r.shadowCastingMode = ShadowCastingMode.Off;
            foreach (var extra in lamp.GetComponentsInChildren<Light>(true)) extra.enabled = false;
            count++;
        }

        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Add Ceiling Lamps", $"Done: {count} lamps added and the scene is saved.", "OK");
    }

    // The pack uses the old built-in "Standard" shader, which shows pink in URP.
    // Switch those materials to URP Lit and carry over the colour, normal and glow textures.
    static void FixMaterialsForURP(GameObject prefab)
    {
        var urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) return;

        foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
            foreach (var mat in r.sharedMaterials)
            {
                if (mat == null || mat.shader == urpLit || !mat.shader.name.StartsWith("Standard")) continue;

                // Read the old values before the shader changes.
                Texture albedo = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                Vector2 tiling = mat.HasProperty("_MainTex") ? mat.GetTextureScale("_MainTex") : Vector2.one;
                Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                Texture normal = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
                Texture glow = mat.HasProperty("_EmissionMap") ? mat.GetTexture("_EmissionMap") : null;
                Color glowColor = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;

                mat.shader = urpLit;
                mat.SetTexture("_BaseMap", albedo);
                mat.SetTextureScale("_BaseMap", tiling);
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Smoothness", 0.4f);

                if (normal != null)
                {
                    mat.SetTexture("_BumpMap", normal);
                    mat.EnableKeyword("_NORMALMAP");
                }

                if (glow != null || glowColor.maxColorComponent > 0.01f)
                {
                    mat.SetTexture("_EmissionMap", glow);
                    mat.SetColor("_EmissionColor", glowColor.maxColorComponent > 0.01f ? glowColor : Color.white);
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }

                EditorUtility.SetDirty(mat);
            }
        AssetDatabase.SaveAssets();
    }
}