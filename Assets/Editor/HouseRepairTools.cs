// Put this file in Assets/Editor/ (next to BringSuccessSceneIntoHouse.cs, or on its own).
// Open House_Bishal, then use the menu: Tools > House Repair > ...
//
//   Seal Under House   - boxes in the space under the house (four sides + a bottom) so you can't
//                        see grass or sky through cracks from the basement and its stairs.
//   Close Wall Seams   - the generated walls, floors and ceilings meet exactly edge-to-edge, which
//                        leaves hairline cracks on screen. This grows each piece by a few millimetres
//                        along its length and height (not its thickness) so neighbours overlap,
//                        and fills the slot under the front door's threshold.
//
// Both are safe to run again: the seal is rebuilt, and the seams are only grown once.
// Nothing is saved automatically: check it in Play mode, then press Cmd+S.
// If someone regenerates "House (generated)", run both again afterwards.

#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HouseRepairTools
{
    // ---- House measurements (from House_Bishal) ----
    const string HouseRootName = "House (generated)";
    // Outside of the outer walls, world X/Z.
    const float HouseXMin = -0.1f, HouseXMax = 18.1f, HouseZMin = -0.1f, HouseZMax = 14.1f;
    // Front doorway (living room): x 14.4-15.6; front wall from z 13.9 (inside) to 14.1 (outside).
    const float DoorXMin = 14.4f, DoorXMax = 15.6f;
    const float FrontWallInsideZ = 13.9f, FrontWallMidZ = 14.0f, FrontWallOutsideZ = 14.1f;

    // ---- Seal settings ----
    const string SealName = "Under-House Seal";
    const float SealThickness = 0.05f;
    const float SealTop = -0.12f;      // tucks up behind the bottom of the ground-floor walls (-0.2)
    const float SealBottom = -3.4f;    // below the basement floor (-3.1)

    // ---- Seam settings ----
    const float SeamOverlap = 0.005f;  // 5 mm added at each end
    const string SeamMarker = "(seams closed)";
    static readonly string[] SeamPieceKinds = { "Wall", "Floor", "Ceiling", "Lintel", "Roof" };

    [MenuItem("Tools/House Repair/Seal Under House")]
    static void SealUnderHouse()
    {
        var house = SceneManager.GetActiveScene();
        var roots = house.GetRootGameObjects();
        if (!roots.Any(g => g.name == HouseRootName))
        {
            Dialog($"Couldn't find \"{HouseRootName}\" in this scene. Open House_Bishal first.");
            return;
        }

        var old = roots.FirstOrDefault(g => g.name == SealName);
        if (old != null) Undo.DestroyObjectImmediate(old);

        var seal = new GameObject(SealName);
        Undo.RegisterCreatedObjectUndo(seal, "Seal Under House");
        SceneManager.MoveGameObjectToScene(seal, house);

        Material mat = FindWallMaterial(roots, "Outside");
        float t = SealThickness, h = SealTop - SealBottom, cy = (SealTop + SealBottom) / 2f;
        float x0 = HouseXMin, x1 = HouseXMax, z0 = HouseZMin, z1 = HouseZMax;

        // Just outside the outer walls, so it never overlaps the basement's own walls and flickers.
        MakeBox(seal.transform, "Front", new Vector3((x0 + x1) / 2f, cy, z1 + t / 2f), new Vector3(x1 - x0 + 2 * t, h, t), mat, false);
        MakeBox(seal.transform, "Back", new Vector3((x0 + x1) / 2f, cy, z0 - t / 2f), new Vector3(x1 - x0 + 2 * t, h, t), mat, false);
        MakeBox(seal.transform, "Left", new Vector3(x0 - t / 2f, cy, (z0 + z1) / 2f), new Vector3(t, h, z1 - z0), mat, false);
        MakeBox(seal.transform, "Right", new Vector3(x1 + t / 2f, cy, (z0 + z1) / 2f), new Vector3(t, h, z1 - z0), mat, false);
        MakeBox(seal.transform, "Bottom", new Vector3((x0 + x1) / 2f, SealBottom - t / 2f, (z0 + z1) / 2f),
            new Vector3(x1 - x0 + 2 * t, t, z1 - z0 + 2 * t), mat, false);

        EditorSceneManager.MarkSceneDirty(house);
        Selection.activeGameObject = seal;
        Dialog("Sealed the space under the house (walls all around and a bottom), so cracks near the basement " +
               "and its stairs show wall instead of grass.\n\nPress Play to check, then Cmd+S to save.");
    }

    [MenuItem("Tools/House Repair/Close Wall Seams")]
    static void CloseWallSeams()
    {
        var house = SceneManager.GetActiveScene();
        var roots = house.GetRootGameObjects();
        var houseRoot = roots.FirstOrDefault(g => g.name == HouseRootName);
        if (houseRoot == null)
        {
            Dialog($"Couldn't find \"{HouseRootName}\" in this scene. Open House_Bishal first.");
            return;
        }
        if (houseRoot.transform.Find(SeamMarker) != null)
        {
            Dialog("The seams have already been closed in this scene.");
            return;
        }

        int grown = 0;
        foreach (var tr in houseRoot.GetComponentsInChildren<Transform>(true))
        {
            if (!SeamPieceKinds.Any(k => tr.name.StartsWith(k))) continue;
            if (tr.GetComponent<MeshFilter>() == null) continue;
            if (Quaternion.Angle(tr.rotation, Quaternion.identity) > 0.01f) continue;  // skip ramps and anything turned
            if (tr.lossyScale != tr.localScale) continue;                                 // only pieces under unscaled parents

            Vector3 s = tr.localScale;
            float thin = Mathf.Min(s.x, s.y, s.z);
            // Grow every direction except the thickness (the smallest one).
            Vector3 add = new Vector3(
                s.x > thin + 1e-4f ? 2 * SeamOverlap : 0f,
                s.y > thin + 1e-4f ? 2 * SeamOverlap : 0f,
                s.z > thin + 1e-4f ? 2 * SeamOverlap : 0f);
            if (add == Vector3.zero) continue;

            Undo.RecordObject(tr, "Close Wall Seams");
            tr.localScale = s + add;
            grown++;
        }

        // Fill the slot under the front door's threshold (below floor level), which opened under the house.
        var sill = MakeBox(houseRoot.transform, "Front Door Threshold Fill",
            new Vector3((DoorXMin + DoorXMax) / 2f, -0.1f, FrontWallMidZ),
            new Vector3(DoorXMax - DoorXMin + 0.01f, 0.21f, FrontWallOutsideZ - FrontWallInsideZ),
            FindWallMaterial(roots, "Outside"), true);
        Undo.RegisterCreatedObjectUndo(sill, "Close Wall Seams");

        var marker = new GameObject(SeamMarker);
        Undo.RegisterCreatedObjectUndo(marker, "Close Wall Seams");
        marker.transform.SetParent(houseRoot.transform, false);

        EditorSceneManager.MarkSceneDirty(house);
        Dialog($"Made {grown} walls, floors and ceilings overlap their neighbours by {SeamOverlap * 1000:0} mm, " +
               "and filled the slot under the front door's threshold.\n\n" +
               "Press Play and check the spots where you saw slivers, then Cmd+S to save.\n" +
               "(Edit > Undo reverses it if anything looks off.)");
    }

    // ---- Basement stair opening ----
    // The hole in the ground floor above Stairs B (measured from House_Bishal): x 8.0-12.4, z 0-1.5.
    // It touches the back wall, so the terrain square that straddles that wall still covered part of
    // the opening (the green sliver). This cuts every terrain square that touches the opening, and
    // covers the part of those squares that's outside the house with a flat ground patch.
    const float StairXMin = 8.0f, StairXMax = 12.4f, StairZMin = 0.0f, StairZMax = 1.5f;
    const string PatchName = "Ground Patch (behind basement stairs)";

    [MenuItem("Tools/House Repair/Fix Basement Stair Opening")]
    static void FixBasementStairOpening()
    {
        var house = SceneManager.GetActiveScene();
        var terrain = house.GetRootGameObjects()
            .SelectMany(g => g.GetComponentsInChildren<Terrain>(true))
            .FirstOrDefault();
        if (terrain == null)
        {
            Dialog("Couldn't find a terrain in this scene.");
            return;
        }

        var td = terrain.terrainData;
        Undo.RegisterCompleteObjectUndo(td, "Fix Basement Stair Opening");
        Vector3 tp = terrain.transform.position;
        Vector3 size = td.size;
        int hr = td.holesResolution;
        float cx = size.x / hr, cz = size.z / hr;
        bool[,] solid = td.GetHoles(0, 0, hr, hr);

        var outsidePieces = new System.Collections.Generic.List<Rect>();
        int cut = 0;
        const float pad = 0.02f;
        for (int z = 0; z < hr; z++)
        {
            float z0 = tp.z + z * cz, z1 = z0 + cz;
            if (z1 < StairZMin - pad || z0 > StairZMax + pad) continue;
            for (int x = 0; x < hr; x++)
            {
                float x0 = tp.x + x * cx, x1 = x0 + cx;
                if (x1 < StairXMin - pad || x0 > StairXMax + pad) continue;
                if (!solid[z, x]) continue;   // already a hole
                solid[z, x] = false;
                cut++;

                // Parts of this square that are outside the house walls need covering.
                var cell = Rect.MinMaxRect(x0, z0, x1, z1);
                outsidePieces.AddRange(Subtract(cell, Rect.MinMaxRect(HouseXMin, HouseZMin, HouseXMax, HouseZMax)));
            }
        }

        if (cut == 0)
        {
            Dialog("The terrain over the basement stairs is already cut out, so the green there must come from " +
                   "somewhere else. Send Claude a screenshot.");
            return;
        }

        // Ground height outside, measured before cutting.
        float groundY = outsidePieces.Count > 0
            ? outsidePieces.Average(r => terrain.SampleHeight(new Vector3(r.center.x, 0f, r.center.y))) + tp.y
            : 0f;

        td.SetHoles(0, 0, solid);
        terrain.Flush();
        EditorUtility.SetDirty(td);

        // Cover the outside bits with flat ground (with a collider so players don't fall in).
        var oldPatch = house.GetRootGameObjects().FirstOrDefault(g => g.name == PatchName);
        if (oldPatch != null) Undo.DestroyObjectImmediate(oldPatch);
        if (outsidePieces.Count > 0)
        {
            var patch = new GameObject(PatchName);
            Undo.RegisterCreatedObjectUndo(patch, "Fix Basement Stair Opening");
            SceneManager.MoveGameObjectToScene(patch, house);
            var mat = GroundMaterial(td, house);
            const float depth = 0.3f;
            int i = 0;
            foreach (var r in outsidePieces)
            {
                MakeBox(patch.transform, "Patch " + (++i),
                    new Vector3(r.center.x, groundY - depth / 2f, r.center.y),
                    new Vector3(r.width, depth, r.height), mat, true);
            }
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(house);
        Dialog($"Cut {cut} terrain squares over the basement stairs so the green strip is gone." +
               (outsidePieces.Count > 0 ? "\n\nThe part of those squares behind the house is covered with a flat ground patch " +
                                          $"(\"{PatchName}\"). If its colour doesn't quite match, drag a different material onto it." : "") +
               "\n\nPress Play to check, then Cmd+S to save.");
    }

    // A material that looks like the terrain's first ground layer, saved next to the scene.
    static Material GroundMaterial(TerrainData td, Scene house)
    {
        var layer = td.terrainLayers != null && td.terrainLayers.Length > 0 ? td.terrainLayers[0] : null;
        string dir = System.IO.Path.GetDirectoryName(house.path).Replace('\\', '/');
        string path = dir + "/House_Bishal_GroundPatch.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var mat = new Material(shader);
        if (layer != null && layer.diffuseTexture != null)
        {
            mat.mainTexture = layer.diffuseTexture;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", layer.diffuseTexture);
            var tiling = new Vector2(1f / Mathf.Max(0.01f, layer.tileSize.x), 1f / Mathf.Max(0.01f, layer.tileSize.y));
            mat.mainTextureScale = tiling;
            if (mat.HasProperty("_BaseMap")) mat.SetTextureScale("_BaseMap", tiling);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
        }
        else
        {
            mat.color = new Color(0.25f, 0.35f, 0.18f);
        }
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // Parts of rect a that are outside rect b (up to 4 pieces).
    static System.Collections.Generic.IEnumerable<Rect> Subtract(Rect a, Rect b)
    {
        if (!a.Overlaps(b)) { yield return a; yield break; }
        if (a.yMin < b.yMin) yield return Rect.MinMaxRect(a.xMin, a.yMin, a.xMax, b.yMin);
        if (a.yMax > b.yMax) yield return Rect.MinMaxRect(a.xMin, b.yMax, a.xMax, a.yMax);
        float y0 = Mathf.Max(a.yMin, b.yMin), y1 = Mathf.Min(a.yMax, b.yMax);
        if (a.xMin < b.xMin) yield return Rect.MinMaxRect(a.xMin, y0, b.xMin, y1);
        if (a.xMax > b.xMax) yield return Rect.MinMaxRect(b.xMax, y0, a.xMax, y1);
    }

    // ---------- Helpers ----------

    static GameObject MakeBox(Transform parent, string name, Vector3 worldCenter, Vector3 size, Material mat, bool keepCollider)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = worldCenter;
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = size;
        if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
        var r = go.GetComponent<Renderer>();
        if (mat != null) r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    // Material of one of the house's front walls on the given side (e.g. "Outside").
    static Material FindWallMaterial(GameObject[] roots, string side)
    {
        var wall = roots.SelectMany(g => g.GetComponentsInChildren<Renderer>(true))
            .FirstOrDefault(r => r.name.StartsWith("Wall h14") && r.name.EndsWith(side));
        return wall != null ? wall.sharedMaterial : null;
    }

    static void Dialog(string msg) => EditorUtility.DisplayDialog("House Repair", msg, "OK");
}
#endif
