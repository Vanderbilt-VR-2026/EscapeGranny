// RoleCardModelBuilder.cs
//
// Builds the character preview on the right of the role select screen, like Fortnite's locker: the
// picked role's 3D character turns slowly on a round stand, lit from behind in the role's colour.
//   Granny    the team's Granny model (Assets/Avatars/GrannyAvatar.fbx with GrannySkin)
//   Prisoner  a PLACEHOLDER: a black silhouette (head, shoulders, body), the "character not
//             revealed yet" look. Swap in the real model when it's ready by giving
//             PrisonerModelPath a prefab/FBX path.
// RoleSelector shows only the picked role's preview.
//
// How to use: runs automatically from Tools > Escape Granny > Build Main Menu (MainMenuSceneBuilder).
// If a model faces the wrong way at the start of its turn, change its yaw below and rebuild.

using UnityEditor;
using UnityEngine;

public static class RoleCardModelBuilder
{
    const string GrannyModelPath = "Assets/Avatars/GrannyAvatar.fbx";
    const string GrannyMaterialPath = "Assets/Avatars/GrannySkin.mat";
    const string PrisonerModelPath = ""; // empty = use the silhouette placeholder
    const string SilhouetteMaterialPath = "Assets/_Game/Materials/MenuSilhouette.mat";
    const string StandMaterialPath = "Assets/_Game/Materials/MenuStand.mat";

    // Turn around the vertical axis so the model faces the player (degrees). Most character models
    // face +z; the player looks at the menu from its -z side, hence 180.
    const float GrannyYaw = 180f;
    const float PrisonerYaw = 180f;
    const float SpinDegreesPerSecond = 30f; // one full turn every 12 s

    // Space kept free at the bottom of the stage for the stand, and in front of the menu (UI units).
    const float StandHeight = 24f;
    const float Padding = 10f;
    const float FrontOffset = 60f;

    // Fills the two preview stages. Returns how many models were added. The caller saves the scene.
    public static int AddModels(RectTransform grannyStage, RectTransform prisonerStage, Color grannyRim, Color prisonerRim)
    {
        int added = 0;

        var grannyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GrannyModelPath);
        if (grannyPrefab == null)
            Debug.LogWarning("[RoleCardModelBuilder] Couldn't find the Granny model at " + GrannyModelPath + ". Skipped it.");
        else
        {
            var granny = (GameObject)PrefabUtility.InstantiatePrefab(grannyPrefab, grannyStage.gameObject.scene);
            var skin = AssetDatabase.LoadAssetAtPath<Material>(GrannyMaterialPath);
            if (skin != null)
                foreach (var r in granny.GetComponentsInChildren<Renderer>())
                {
                    var slots = r.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++) slots[i] = skin;
                    r.sharedMaterials = slots;
                }
            PlaceOnStand(granny, grannyStage, GrannyYaw, grannyRim);
            added++;
        }

        var prisonerPrefab = string.IsNullOrEmpty(PrisonerModelPath)
            ? null : AssetDatabase.LoadAssetAtPath<GameObject>(PrisonerModelPath);
        var prisoner = prisonerPrefab != null
            ? (GameObject)PrefabUtility.InstantiatePrefab(prisonerPrefab, prisonerStage.gameObject.scene)
            : BuildSilhouette();
        PlaceOnStand(prisoner, prisonerStage, PrisonerYaw, prisonerRim);
        added++;
        return added;
    }

    // Fits the model into the stage (keeping its proportions), standing on a round stand at the
    // bottom, a little in front of the menu, turning like a turntable.
    static void PlaceOnStand(GameObject model, RectTransform stage, float yaw, Color rim)
    {
        model.name = "Model";
        // For display only: nothing that falls, collides or catches the controller ray.
        foreach (var rb in model.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
        foreach (var col in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);

        model.transform.position = Vector3.zero;
        model.transform.rotation = stage.rotation * Quaternion.Euler(0f, yaw, 0f) * model.transform.rotation;

        var rect = stage.rect;
        float uiToWorld = stage.lossyScale.x;
        var bounds = GetBounds(model);
        float fit = Mathf.Min((rect.width - 2f * Padding) * uiToWorld / Mathf.Max(bounds.size.x, 0.0001f),
                              (rect.height - Padding - StandHeight) * uiToWorld / Mathf.Max(bounds.size.y, 0.0001f));
        model.transform.localScale *= fit;
        bounds = GetBounds(model);

        // Feet on the stand: bottom-centre of the model goes to the stand's top, in front of the menu.
        float depth = Mathf.Max(bounds.size.x, bounds.size.z) / uiToWorld; // it turns, so its widest side
        var feet = stage.TransformPoint(new Vector3(rect.center.x, rect.yMin + StandHeight, -(depth / 2f + FrontOffset)));
        var bottomCentre = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        model.transform.position += feet - bottomCentre;
        model.transform.SetParent(stage, true);

        var idle = model.AddComponent<MenuModelIdle>();
        var so = new SerializedObject(idle);
        so.FindProperty("spinDegreesPerSecond").floatValue = SpinDegreesPerSecond;
        so.FindProperty("breathe").floatValue = 0.012f;
        so.ApplyModifiedPropertiesWithoutUndo();

        AddStand(stage, feet, depth * uiToWorld);
        AddRimLight(stage, feet, rim);
    }

    // A dark round stand under the feet (it doesn't turn; only the character does).
    static void AddStand(RectTransform stage, Vector3 feet, float modelWidth)
    {
        var stand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stand.name = "Stand";
        Object.DestroyImmediate(stand.GetComponent<Collider>());
        float diameter = Mathf.Max(modelWidth * 1.3f, 0.15f);
        const float thickness = 0.02f;
        stand.transform.localScale = new Vector3(diameter, thickness / 2f, diameter); // a cylinder is 2 units tall
        stand.transform.position = feet - Vector3.up * thickness / 2f;
        stand.GetComponent<Renderer>().sharedMaterial = LoadOrCreateMaterial(StandMaterialPath,
            "Universal Render Pipeline/Lit", new Color(0.11f, 0.13f, 0.12f), 0.7f);
        stand.transform.SetParent(stage, true);
    }

    // Coloured light behind and above the character: outlines it against the dark menu.
    static void AddRimLight(RectTransform stage, Vector3 feet, Color color)
    {
        var go = new GameObject("RimLight");
        go.transform.SetParent(stage, false);
        go.transform.position = feet + stage.up * 0.45f + stage.forward * 0.25f; // forward = away from the player
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = 3f;
        light.range = 0.8f; // metres: only reaches this character
        light.shadows = LightShadows.None;
    }

    // Head, shoulders and body from primitives, in an unlit black material: a "?" character.
    static GameObject BuildSilhouette()
    {
        var root = new GameObject("Model");
        var material = LoadOrCreateMaterial(SilhouetteMaterialPath, "Universal Render Pipeline/Unlit", Color.black, 0f);
        void Part(PrimitiveType type, Vector3 position, Vector3 scale, Vector3 euler)
        {
            var part = GameObject.CreatePrimitive(type);
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.transform.localEulerAngles = euler;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }
        Part(PrimitiveType.Capsule, new Vector3(0f, 0.55f, 0f), new Vector3(0.42f, 0.55f, 0.28f), Vector3.zero);     // body
        Part(PrimitiveType.Capsule, new Vector3(0f, 0.98f, 0f), new Vector3(0.26f, 0.32f, 0.24f), new Vector3(0, 0, 90)); // shoulders
        Part(PrimitiveType.Sphere, new Vector3(0f, 1.32f, 0f), new Vector3(0.3f, 0.34f, 0.3f), Vector3.zero);          // head
        return root;
    }

    static Material LoadOrCreateMaterial(string path, string shader, Color color, float smoothness)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;

        if (!AssetDatabase.IsValidFolder("Assets/_Game/Materials")) AssetDatabase.CreateFolder("Assets/_Game", "Materials");
        material = new Material(Shader.Find(shader));
        material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static Bounds GetBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.1f);

        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        return bounds;
    }
}
