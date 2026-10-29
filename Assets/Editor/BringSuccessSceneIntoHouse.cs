// Put this file in Assets/Editor/ (create the folder if it doesn't exist).
// Then open House_Bishal and click: Tools > Escape Granny > Bring Success Scene Into House
//
// What it does (in House_Bishal only; Success_Scene is left untouched):
//  1. Copies Success_Scene's nature (terrain, trees, rocks, cliffs...) around the house.
//  2. Copies the chained door + padlock + chains + exit zone into the house's front doorway.
//  3. Puts the key on a small table just inside the front door (for testing).
//  4. Copies the "Congratulations" canvas.
//  5. Turns off the old EscapeTrigger (which loaded Success_Scene).
// The terrain gets its own copy (House_Bishal_Terrain.asset, next to the scene),
// flattened around the house and with a hole under it so the basement stairs still work.
// Nothing is saved automatically: look it over, then press Cmd+S.

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BringSuccessSceneIntoHouse
{
    const string HouseSceneName = "House_Bishal";
    const string SuccessSceneName = "Success_Scene";
    const string DoorGroupName = "Escape Door (from Success_Scene)";
    const string NatureName = "Nature (from Success_Scene)";
    const string CanvasName = "CongratsCanvas";

    // House front doorway (ground floor, living room), at floor level.
    static readonly Vector3 HouseDoorway = new Vector3(15f, 0f, 14f);
    // Where the doorway sits inside Success_Scene's "Room" object.
    static readonly Vector3 RoomDoorwayLocal = new Vector3(0f, 0f, 2f);
    // Outside of the house walls in world X/Z (xMin, zMin, xMax, zMax).
    static readonly Rect HouseFootprint = Rect.MinMaxRect(-0.1f, -0.1f, 18.1f, 14.1f);
    const float HouseFloorY = 0f;

    // Testing setup: table + key just inside the door, to the right (Room-local coordinates).
    static readonly Vector3 TableLocal = new Vector3(1.5f, 0.4f, 0.3f);
    static readonly Vector3 KeyLocal = new Vector3(1.5f, 0.85f, 0.3f);

    // Parts of the little success room we don't want inside the house.
    static readonly string[] RoomPartsToRemove =
    {
        "Wall_Front_L", "Wall_Front_R", "Wall_Front_Top", "Wall_Left", "Wall_Right",
        "Wall_Back", "Ceiling", "Point Light", "SpawnPoint"
    };

    // Groups of placed nature objects that may need moving/removing near the house.
    static readonly string[] ScatterGroups = { "Trees", "Bushes", "Rocks", "Cliffs" };

    [MenuItem("Tools/Escape Granny/Bring Success Scene Into House")]
    static void Run()
    {
        var house = SceneManager.GetActiveScene();
        if (house.name != HouseSceneName)
        {
            Dialog($"Open {HouseSceneName} first (double-click it in the Project window), then run this again.");
            return;
        }
        if (house.GetRootGameObjects().Any(g => g.name == DoorGroupName))
        {
            Dialog($"This has already been done in this scene.\n\nTo redo it, delete \"{DoorGroupName}\", " +
                   $"\"{NatureName}\" and \"{CanvasName}\" from the Hierarchy first.");
            return;
        }

        // Look at every scene and match the name loosely (ignores case, spaces, underscores, dashes).
        string successPath = AssetDatabase.FindAssets("t:Scene")
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(p => Simplify(Path.GetFileNameWithoutExtension(p)) == Simplify(SuccessSceneName));
        if (successPath == null)
        {
            // Let the user pick it by hand.
            string picked = EditorUtility.OpenFilePanel("Pick the success scene", Application.dataPath, "unity");
            if (string.IsNullOrEmpty(picked)) return;
            picked = picked.Replace('\\', '/');
            string projectRoot = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/') + "/";
            if (!picked.StartsWith(projectRoot))
            {
                Dialog("That scene isn't inside this Unity project.");
                return;
            }
            successPath = picked.Substring(projectRoot.Length);
        }

        if (house.isDirty && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var notes = new List<string>();
        var success = EditorSceneManager.OpenScene(successPath, OpenSceneMode.Additive);
        try
        {
            var roots = success.GetRootGameObjects();
            var room = roots.FirstOrDefault(g => g.name == "Room");
            var env = roots.FirstOrDefault(g => g.name == "Environment");
            var canvas = roots.FirstOrDefault(g => g.name == CanvasName);
            if (room == null || env == null)
            {
                Dialog($"{SuccessSceneName} is missing its \"Room\" or \"Environment\" object, so nothing was changed.");
                return;
            }

            // Duplicate everything in one go so references between them are kept.
            var originals = new[] { room, env, canvas }.Where(g => g != null).ToArray();
            Selection.objects = originals;
            Unsupported.DuplicateGameObjectsUsingPasteboard();
            var copies = Selection.gameObjects.Where(c => !originals.Contains(c)).ToArray();

            var rootMap = new Dictionary<GameObject, GameObject>();
            foreach (var o in originals)
            {
                var c = copies.FirstOrDefault(x => BaseName(x.name) == o.name && !rootMap.ContainsValue(x));
                if (c != null) rootMap[o] = c;
            }
            if (!rootMap.ContainsKey(room) || !rootMap.ContainsKey(env))
            {
                foreach (var c in copies) Object.DestroyImmediate(c);
                Dialog("Copying from Success_Scene didn't work, so nothing was changed. Tell Claude about this message.");
                return;
            }

            foreach (var c in rootMap.Values) SceneManager.MoveGameObjectToScene(c, house);
            RemapReferences(rootMap, success);

            var door = rootMap[room];
            var nature = rootMap[env];
            rootMap.TryGetValue(canvas, out var congrats);
            door.name = DoorGroupName;
            nature.name = NatureName;
            if (congrats != null) congrats.name = CanvasName;

            // --- Door: line the Room's doorway up with the house's front doorway ---
            Vector3 doorGroupPos = HouseDoorway - RoomDoorwayLocal;
            Vector3 shift = doorGroupPos - room.transform.position;
            door.transform.SetPositionAndRotation(doorGroupPos, Quaternion.identity);

            foreach (var part in RoomPartsToRemove)
            {
                var t = door.transform.Find(part);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }

            var table = door.transform.Find("Table");
            if (table != null) table.localPosition = TableLocal;
            var key = door.transform.Find("Key_Root");
            if (key != null) key.localPosition = KeyLocal;
            else notes.Add("Couldn't find Key_Root, so the key wasn't moved.");

            // --- Nature: move it by the same amount so it lines up with the door ---
            nature.transform.position = env.transform.position + shift;

            bool houseHasSun = house.GetRootGameObjects()
                .Where(g => g != nature)
                .SelectMany(g => g.GetComponentsInChildren<Light>(true))
                .Any(l => l.type == LightType.Directional);
            if (houseHasSun)
            {
                foreach (var l in nature.GetComponentsInChildren<Light>(true).Where(l => l.type == LightType.Directional))
                {
                    l.gameObject.SetActive(false);
                    notes.Add($"Turned off the nature's \"{l.name}\" so the house keeps its own lighting.");
                }
            }

            var terrain = nature.GetComponentInChildren<Terrain>(true);
            if (terrain != null) FitTerrainAroundHouse(terrain, nature, house, notes);
            else notes.Add("No terrain found in the nature, so nothing was flattened.");

            // --- Old trigger that loaded Success_Scene is no longer needed ---
            foreach (var t in house.GetRootGameObjects()
                         .SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                         .Where(t => t.name == "EscapeTrigger"))
            {
                t.gameObject.SetActive(false);
                notes.Add("Turned off the old EscapeTrigger (the door's ExitZone handles escaping now).");
            }

            if (notes.Count > 0) Debug.Log("[Escape Granny] Notes:\n- " + string.Join("\n- ", notes));
            EditorSceneManager.MarkSceneDirty(house);
            Selection.activeGameObject = door;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();

            Dialog("Done! The chained door is in the front doorway, the key is on a table just inside, " +
                   "and the nature is around the house.\n\n" +
                   (notes.Count > 0 ? "- " + string.Join("\n- ", notes) + "\n\n" : "") +
                   "Look it over, then press Cmd+S to save.");
        }
        finally
        {
            if (success.isLoaded) EditorSceneManager.CloseScene(success, true);
        }
    }

    // Second step: hide individual plants/rocks (not whole groups) that end up inside the house.
    [MenuItem("Tools/Escape Granny/Hide Nature Inside House")]
    static void HideNatureInsideHouse()
    {
        var house = SceneManager.GetActiveScene();
        var nature = house.GetRootGameObjects().FirstOrDefault(g => g.name == NatureName);
        if (nature == null)
        {
            Dialog($"Couldn't find \"{NatureName}\" in this scene. Run \"Bring Success Scene Into House\" first.");
            return;
        }

        var inside = Expand(HouseFootprint, 0.3f);
        const float houseBottom = -3.3f, houseTop = 6.2f;
        var hidden = new List<string>();
        var tooBig = new List<string>();

        foreach (var r in nature.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || !r.gameObject.activeInHierarchy) continue;
            if (r.GetComponentInParent<Terrain>() != null) continue;
            var b = r.bounds;
            var rect = Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z);
            if (!rect.Overlaps(inside) || b.max.y < houseBottom || b.min.y > houseTop) continue;

            if (b.size.x > 30f || b.size.z > 30f) { tooBig.Add(r.name); continue; }

            // Hide the whole plant/rock (its prefab root), not just one piece of it.
            var target = PrefabUtility.GetOutermostPrefabInstanceRoot(r.gameObject);
            if (target == null || !target.transform.IsChildOf(nature.transform)) target = r.gameObject;
            if (!target.activeSelf) continue;
            target.SetActive(false);
            hidden.Add(target.name);
        }

        EditorSceneManager.MarkSceneDirty(house);
        string msg = hidden.Count == 0 ? "Nothing from the nature was inside the house." :
            $"Hid {hidden.Count} plants/rocks that were inside the house.";
        if (tooBig.Count > 0)
            msg += $"\n\nThese are very large and overlap the house, so they were left alone " +
                   $"(check them in the Scene view):\n- {string.Join("\n- ", tooBig.Distinct().Take(10))}";
        Debug.Log("[Escape Granny] " + msg + "\nHidden: " + string.Join(", ", hidden));
        Dialog(msg + "\n\nPress Cmd+S to save.");
    }

    // Removes painted grass/flowers (terrain details) under and just around the house.
    [MenuItem("Tools/Escape Granny/Clear Grass Inside House")]
    static void ClearGrassInsideHouse()
    {
        var house = SceneManager.GetActiveScene();
        var nature = house.GetRootGameObjects().FirstOrDefault(g => g.name == NatureName);
        var terrain = nature != null ? nature.GetComponentInChildren<Terrain>(true) : null;
        if (terrain == null)
        {
            Dialog("Couldn't find the nature's terrain in this scene.");
            return;
        }

        var td = terrain.terrainData;
        if (td.detailPrototypes.Length == 0)
        {
            Dialog("The terrain has no painted grass, so there's nothing to clear.");
            return;
        }

        Vector3 tp = terrain.transform.position;
        Vector3 size = td.size;
        int res = td.detailResolution;
        var area = Expand(HouseFootprint, 0.5f);
        float cx = size.x / res, cz = size.z / res;
        int cleared = 0;

        for (int layer = 0; layer < td.detailPrototypes.Length; layer++)
        {
            int[,] d = td.GetDetailLayer(0, 0, res, res, layer);
            for (int z = 0; z < res; z++)
            {
                float z0 = tp.z + z * cz;
                if (z0 + cz < area.yMin || z0 > area.yMax) continue;
                for (int x = 0; x < res; x++)
                {
                    float x0 = tp.x + x * cx;
                    if (x0 + cx < area.xMin || x0 > area.xMax) continue;
                    if (d[z, x] != 0) { d[z, x] = 0; cleared++; }
                }
            }
            td.SetDetailLayer(0, 0, layer, d);
        }

        EditorUtility.SetDirty(td);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(house);
        Dialog(cleared > 0 ? "Cleared the grass from inside the house." : "There was no grass inside the house to clear.");
    }

    // ---- Lighting settings (change these numbers and run the menu again to adjust) ----
    const float SunIntensity = 0.8f;                                  // was 0.08
    static readonly Color AmbientColor = new Color(0.30f, 0.30f, 0.34f); // was ~0.035 (nearly black)
    const float FogDensity = 0.035f;                                  // was 0.18 (very thick)
    static readonly Color FogColor = new Color(0.30f, 0.33f, 0.37f);  // was nearly black
    const float MinLampIntensity = 1.8f;                              // room lamps were 0.4-0.9
    const float DoorLightIntensity = 3f;
    const float DoorLightRange = 5f;
    const float PorchLightIntensity = 2f;
    const float PorchLightRange = 6f;

    [MenuItem("Tools/Escape Granny/Brighten Lighting")]
    static void BrightenLighting()
    {
        var house = SceneManager.GetActiveScene();
        var roots = house.GetRootGameObjects();

        // Overall brightness: ambient light and fog.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = AmbientColor;
        RenderSettings.fogDensity = FogDensity;
        RenderSettings.fogColor = FogColor;

        // The house's own sun (not the nature's, which is turned off).
        var nature = roots.FirstOrDefault(g => g.name == NatureName);
        foreach (var sun in roots.Where(g => g != nature)
                     .SelectMany(g => g.GetComponentsInChildren<Light>(true))
                     .Where(l => l.type == LightType.Directional && l.gameObject.activeInHierarchy))
            sun.intensity = SunIntensity;

        // Room lamps.
        var lampGroup = roots.FirstOrDefault(g => g.name == "House Lighting (generated)");
        int lamps = 0;
        if (lampGroup != null)
        {
            foreach (var l in lampGroup.GetComponentsInChildren<Light>(true))
            {
                if (l.intensity < MinLampIntensity) { l.intensity = MinLampIntensity; lamps++; }
            }
        }

        // Lights by the escape door: one inside (to see the key and padlock), one on the porch.
        var door = roots.FirstOrDefault(g => g.name == DoorGroupName);
        if (door != null)
        {
            // Escape Door group sits 2 m inside the doorway, so z = 2 is the doorway itself.
            MakePointLight(door.transform, "Door Light (inside)", new Vector3(0.5f, 2.1f, 0.9f),
                DoorLightIntensity, DoorLightRange, new Color(1f, 0.92f, 0.78f));
            MakePointLight(door.transform, "Porch Light (outside)", new Vector3(0f, 2.4f, 3.0f),
                PorchLightIntensity, PorchLightRange, new Color(1f, 0.88f, 0.7f));
        }

        EditorSceneManager.MarkSceneDirty(house);
        Dialog("Brightened the lighting:\n" +
               $"- Ambient light and fog are lighter\n- Sun set to {SunIntensity}\n" +
               $"- {lamps} room lamps raised to at least {MinLampIntensity}\n" +
               (door != null ? "- Added a light just inside the front door and one on the porch\n" :
                   "- Couldn't find the escape door, so no door lights were added\n") +
               "\nTo adjust, change the numbers near the top of the Lighting section in this script and run it again." +
               "\n\nPress Cmd+S to save.");
    }

    static void MakePointLight(Transform parent, string name, Vector3 localPos, float intensity, float range, Color color)
    {
        var t = parent.Find(name);
        var go = t != null ? t.gameObject : new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var light = go.GetComponent<Light>();
        if (light == null) light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.intensity = intensity;
        light.range = range;
        light.color = color;
        light.shadows = LightShadows.None;
    }

    // Moves Padlock_Root onto the chain in the editor, so it doesn't have to travel there when the game starts.
    [MenuItem("Tools/Escape Granny/Put Padlock On Chain")]
    static void PutPadlockOnChain()
    {
        var house = SceneManager.GetActiveScene();
        var group = house.GetRootGameObjects().FirstOrDefault(g => g.name == DoorGroupName);
        if (group == null)
        {
            Dialog($"Couldn't find \"{DoorGroupName}\" in this scene.");
            return;
        }

        var hanging = group.GetComponentsInChildren<MonoBehaviour>(true)
            .FirstOrDefault(m => m != null && m.GetType().Name == "HangingPadlock");
        if (hanging == null)
        {
            Dialog("Couldn't find the padlock's HangingPadlock script.");
            return;
        }

        var hs = new SerializedObject(hanging);
        var chain = hs.FindProperty("chain")?.objectReferenceValue as MonoBehaviour;
        if (chain == null)
            chain = group.GetComponentsInChildren<MonoBehaviour>(true)
                .FirstOrDefault(m => m != null && m.GetType().Name == "ChainBuilder");
        if (chain == null)
        {
            Dialog("Couldn't find the chain the padlock hangs on.");
            return;
        }

        var cs = new SerializedObject(chain);
        var start = cs.FindProperty("start")?.objectReferenceValue as Transform;
        var end = cs.FindProperty("end")?.objectReferenceValue as Transform;
        float sag = cs.FindProperty("sag")?.floatValue ?? 0.12f;
        if (start == null || end == null)
        {
            Dialog("The chain's Start or End isn't set.");
            return;
        }

        float t = Mathf.Clamp01(hs.FindProperty("chainPosition")?.floatValue ?? 0.5f);
        float hangDepth = hs.FindProperty("hangDepth")?.floatValue ?? 0f;
        var hangPoint = hs.FindProperty("hangPoint")?.objectReferenceValue as Transform;

        // Same curve as ChainBuilder.BaseCurve.
        Vector3 onChain = Vector3.Lerp(start.position, end.position, t) + Vector3.down * (sag * 4f * t * (1f - t));

        var padlock = hanging.transform;
        Undo.RecordObject(padlock, "Put Padlock On Chain");
        if (hangPoint != null)
        {
            // Put the padlock's hang point on the chain.
            padlock.position += onChain - hangPoint.position;
        }
        else
        {
            // Hang the top of the padlock just below the chain.
            float top = TryGetBounds(padlock, out var b) ? b.max.y - padlock.position.y : 0f;
            padlock.position = onChain + Vector3.down * (top + hangDepth);
        }

        EditorSceneManager.MarkSceneDirty(house);
        Selection.activeGameObject = padlock.gameObject;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        Dialog("Moved the padlock onto the chain, so it starts there instead of travelling over.\n\nPress Play to check, then Cmd+S to save.");
    }

    // ---- Door fit settings ----
    // House front doorway (world): x 14.4-15.6, floor 0 to lintel 2.2.
    const float OpeningXMin = 14.4f, OpeningXMax = 15.6f, OpeningTop = 2.2f;
    // House wall: inside face z = 13.9, middle 14.0, outside face 14.1.
    const float WallInsideZ = 13.9f, WallMidZ = 14.0f, WallOutsideZ = 14.1f;
    // How far in front of the inside wall face the chains sit.
    const float ChainGapFromWall = 0.06f;

    [MenuItem("Tools/Escape Granny/Fit Door And Chains")]
    static void FitDoorAndChains()
    {
        var house = SceneManager.GetActiveScene();
        var roots = house.GetRootGameObjects();
        var group = roots.FirstOrDefault(g => g.name == DoorGroupName);
        if (group == null)
        {
            Dialog($"Couldn't find \"{DoorGroupName}\" in this scene.");
            return;
        }
        var notes = new List<string>();

        // --- 1. Measure the door ---
        var doorScript = group.GetComponentsInChildren<MonoBehaviour>(true)
            .FirstOrDefault(m => m != null && m.GetType().Name == "EscapeDoor");
        GameObject door = doorScript != null ? doorScript.gameObject
            : group.transform.Cast<Transform>().FirstOrDefault(t => t.name == "Door")?.gameObject;
        if (door == null)
        {
            Dialog("Couldn't find the door inside the escape door group.");
            return;
        }
        var padlock = group.GetComponentsInChildren<MonoBehaviour>(true)
            .FirstOrDefault(m => m != null && m.GetType().Name == "Padlock");
        var doorRenderers = door.GetComponentsInChildren<Renderer>(true)
            .Where(r => padlock == null || !r.transform.IsChildOf(padlock.transform)).ToArray();
        if (doorRenderers.Length == 0)
        {
            Dialog("The door has no visible parts to measure.");
            return;
        }
        var b = doorRenderers[0].bounds;
        foreach (var r in doorRenderers.Skip(1)) b.Encapsulate(r.bounds);

        // --- 2. Build a frame that fills the gaps, using the house's wall materials ---
        Material inner = FindWallMaterial(roots, "Living Room", "h14");
        Material outer = FindWallMaterial(roots, "Outside", "h14");

        var old = group.transform.Find("Door Frame");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var frame = new GameObject("Door Frame");
        frame.transform.SetParent(group.transform, false);

        const float overlap = 0.015f; // tuck slightly behind the door so no light leaks through
        float leftW = b.min.x - OpeningXMin + overlap;
        float rightW = OpeningXMax - b.max.x + overlap;
        float topH = OpeningTop - b.max.y + overlap;
        int pieces = 0;
        if (leftW > overlap + 0.001f)
            pieces += AddFrameStrip(frame.transform, "Left", OpeningXMin, OpeningXMin + leftW, 0f, OpeningTop, inner, outer);
        if (rightW > overlap + 0.001f)
            pieces += AddFrameStrip(frame.transform, "Right", OpeningXMax - rightW, OpeningXMax, 0f, OpeningTop, inner, outer);
        if (topH > overlap + 0.001f)
            pieces += AddFrameStrip(frame.transform, "Top", OpeningXMin, OpeningXMax, OpeningTop - topH, OpeningTop, inner, outer);
        notes.Add(pieces > 0
            ? $"Added a door frame to cover the gaps (sides: {Mathf.Max(0, leftW - overlap) * 100:0} cm and {Mathf.Max(0, rightW - overlap) * 100:0} cm, top: {Mathf.Max(0, topH - overlap) * 100:0} cm)."
            : "The door already fills the opening, so no frame was needed.");
        if (inner == null || outer == null)
            notes.Add("Couldn't find the house's wall material, so the frame uses the default material. You can drag a material onto it.");

        // --- 3. Move the chains in front of the inside wall face ---
        var chainParts = new[] { "DoorChains", "ChainStart", "ChainEnd" }
            .Select(n => group.transform.Find(n)).Where(t => t != null).ToArray();
        var pattern = group.transform.Find("DoorChains");
        if (pattern != null)
        {
            float targetZ = WallInsideZ - ChainGapFromWall;
            float dz = targetZ - pattern.position.z;
            if (dz < -0.001f)
            {
                foreach (var t in chainParts) t.position += new Vector3(0f, 0f, dz);
                notes.Add($"Moved the chains {-dz * 100:0} cm into the room so they sit in front of the wall.");
            }
            else notes.Add("The chains are already in front of the wall.");
        }
        else notes.Add("Couldn't find DoorChains, so the chains weren't moved.");

        EditorSceneManager.MarkSceneDirty(house);
        Selection.activeGameObject = frame;
        Dialog("- " + string.Join("\n- ", notes) + "\n\nPress Play to check how it looks, then Cmd+S to save.");
    }

    // Two thin layers (inside half and outside half of the wall) so each side matches its wall.
    static int AddFrameStrip(Transform parent, string name, float x0, float x1, float y0, float y1, Material inner, Material outer)
    {
        MakeBox(parent, name + " (inside)", new Vector3((x0 + x1) / 2, (y0 + y1) / 2, (WallInsideZ + WallMidZ) / 2),
            new Vector3(x1 - x0, y1 - y0, WallMidZ - WallInsideZ), inner);
        MakeBox(parent, name + " (outside)", new Vector3((x0 + x1) / 2, (y0 + y1) / 2, (WallMidZ + WallOutsideZ) / 2),
            new Vector3(x1 - x0, y1 - y0, WallOutsideZ - WallMidZ), outer);
        return 1;
    }

    static void MakeBox(Transform parent, string name, Vector3 worldCenter, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = worldCenter;
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = size;
        if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static Material FindWallMaterial(GameObject[] roots, string room, string wallLine)
    {
        var wall = roots.SelectMany(g => g.GetComponentsInChildren<Renderer>(true))
            .FirstOrDefault(r => r.name.StartsWith("Wall " + wallLine) && r.name.EndsWith(room));
        return wall != null ? wall.sharedMaterial : null;
    }

    // ---------- Terrain ----------

    static void FitTerrainAroundHouse(Terrain terrain, GameObject nature, Scene house, List<string> notes)
    {
        // Give the house its own copy of the terrain so Success_Scene isn't affected.
        var src = terrain.terrainData;
        string dir = Path.GetDirectoryName(house.path).Replace('\\', '/');
        string dst = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{HouseSceneName}_Terrain.asset");
        string srcPath = AssetDatabase.GetAssetPath(src);
        TerrainData td;
        if (!string.IsNullOrEmpty(srcPath) && srcPath.EndsWith(".asset") && AssetDatabase.CopyAsset(srcPath, dst))
        {
            td = AssetDatabase.LoadAssetAtPath<TerrainData>(dst);
        }
        else
        {
            td = Object.Instantiate(src);
            AssetDatabase.CreateAsset(td, dst);
        }
        terrain.terrainData = td;
        var col = terrain.GetComponent<TerrainCollider>();
        if (col != null) col.terrainData = td;
        notes.Add($"Made a separate terrain for the house: {dst}");

        Vector3 tp = terrain.transform.position;
        Vector3 size = td.size;

        if (HouseFootprint.xMin < tp.x || HouseFootprint.xMax > tp.x + size.x ||
            HouseFootprint.yMin < tp.z || HouseFootprint.yMax > tp.z + size.z)
            notes.Add("Warning: part of the house is past the edge of the terrain.");

        // Remember where nature objects sit relative to the ground before flattening.
        var scatter = ScatterGroups
            .Select(n => nature.transform.Find(n))
            .Where(t => t != null)
            .SelectMany(g => g.Cast<Transform>())
            .ToList();
        var groundBefore = scatter.ToDictionary(t => t, t => terrain.SampleHeight(t.position));

        // Remove nature objects that would poke into the house.
        var keepOut = Expand(HouseFootprint, 1f);
        int removed = 0;
        foreach (var t in scatter)
        {
            if (!TryGetBounds(t, out var b)) continue;
            var r = Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z);
            if (!r.Overlaps(keepOut)) continue;
            if (b.size.x > 40f || b.size.z > 40f)
            {
                notes.Add($"\"{t.name}\" is large and overlaps the house; check it and move or hide it if needed.");
                continue;
            }
            t.gameObject.SetActive(false);
            removed++;
        }
        if (removed > 0) notes.Add($"Hid {removed} trees/rocks/bushes that were inside the house.");

        var trees = td.treeInstances;
        var keptTrees = trees.Where(ti =>
        {
            var wp = Vector3.Scale(ti.position, size) + tp;
            return !keepOut.Contains(new Vector2(wp.x, wp.z));
        }).ToArray();
        if (keptTrees.Length != trees.Length)
        {
            td.treeInstances = keptTrees;
            notes.Add($"Removed {trees.Length - keptTrees.Length} painted terrain trees inside the house.");
        }

        // Flatten the ground around the house to floor level, blending out to the original hills.
        int res = td.heightmapResolution;
        float[,] h = td.GetHeights(0, 0, res, res);
        float target = (HouseFloorY - 0.03f - tp.y) / size.y;
        if (target < 0f || target > 1f)
            notes.Add("Warning: the terrain can't reach the house's floor height, so the ground near the door may not line up.");
        target = Mathf.Clamp01(target);

        var flat = Expand(HouseFootprint, 1.5f);
        const float blend = 8f;
        for (int z = 0; z < res; z++)
        {
            float wz = tp.z + z / (float)(res - 1) * size.z;
            for (int x = 0; x < res; x++)
            {
                float wx = tp.x + x / (float)(res - 1) * size.x;
                float d = DistanceToRect(flat, wx, wz);
                if (d >= blend) continue;
                h[z, x] = Mathf.Lerp(target, h[z, x], Mathf.SmoothStep(0f, 1f, d / blend));
            }
        }
        td.SetHeights(0, 0, h);

        // Cut a hole under the house so the terrain doesn't block the basement or its stairs.
        int hr = td.holesResolution;
        bool[,] solid = td.GetHoles(0, 0, hr, hr);
        float cx = size.x / hr, cz = size.z / hr;
        var inside = Expand(HouseFootprint, -0.05f);
        for (int z = 0; z < hr; z++)
        {
            float z0 = tp.z + z * cz;
            for (int x = 0; x < hr; x++)
            {
                float x0 = tp.x + x * cx;
                if (x0 >= inside.xMin && x0 + cx <= inside.xMax && z0 >= inside.yMin && z0 + cz <= inside.yMax)
                    solid[z, x] = false;
            }
        }
        td.SetHoles(0, 0, solid);
        terrain.Flush();

        // Put nature objects back on the (now flattened) ground.
        foreach (var t in scatter)
        {
            if (!t.gameObject.activeSelf) continue;
            float dy = terrain.SampleHeight(t.position) - groundBefore[t];
            if (Mathf.Abs(dy) > 0.001f) t.position += Vector3.up * dy;
        }

        EditorUtility.SetDirty(td);
        AssetDatabase.SaveAssets();
    }

    // ---------- Helpers ----------

    // After moving copies to the house, point any leftover references at the copies, not the originals.
    static void RemapReferences(Dictionary<GameObject, GameObject> rootMap, Scene from)
    {
        foreach (var copyRoot in rootMap.Values)
        {
            foreach (var comp in copyRoot.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (comp == null) continue;
                var so = new SerializedObject(comp);
                var it = so.GetIterator();
                bool changed = false;
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var o = it.objectReferenceValue;
                    if (o == null) continue;
                    GameObject go = o is GameObject g ? g : o is Component c ? c.gameObject : null;
                    if (go == null || go.scene != from) continue;

                    var mapped = MapAcross(go, rootMap);
                    if (mapped == null) continue;
                    if (o is GameObject) it.objectReferenceValue = mapped;
                    else
                    {
                        var oc = (Component)o;
                        int idx = System.Array.IndexOf(oc.gameObject.GetComponents(oc.GetType()), oc);
                        var candidates = mapped.GetComponents(oc.GetType());
                        if (idx >= 0 && idx < candidates.Length) it.objectReferenceValue = candidates[idx];
                    }
                    changed = true;
                }
                if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    static GameObject MapAcross(GameObject go, Dictionary<GameObject, GameObject> rootMap)
    {
        var root = go.transform.root.gameObject;
        if (!rootMap.TryGetValue(root, out var copyRoot)) return null;
        if (go == root) return copyRoot;
        string path = AnimationUtility.CalculateTransformPath(go.transform, root.transform);
        var t = copyRoot.transform.Find(path);
        return t != null ? t.gameObject : null;
    }

    static bool TryGetBounds(Transform t, out Bounds b)
    {
        b = default;
        bool any = false;
        foreach (var r in t.GetComponentsInChildren<Renderer>(true))
        {
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        return any;
    }

    static Rect Expand(Rect r, float m) => Rect.MinMaxRect(r.xMin - m, r.yMin - m, r.xMax + m, r.yMax + m);

    static float DistanceToRect(Rect r, float x, float z)
    {
        float dx = Mathf.Max(r.xMin - x, 0f, x - r.xMax);
        float dz = Mathf.Max(r.yMin - z, 0f, z - r.yMax);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    static string Simplify(string n) => Regex.Replace(n.ToLowerInvariant(), @"[\s_\-]", "");

    static string BaseName(string n) => Regex.Replace(n, @"\s*\(\d+\)$", "");

    static void Dialog(string msg) => EditorUtility.DisplayDialog("Escape Granny", msg, "OK");
}
#endif
