// WallGrimeDecorator.cs
//
// Scatters blood splatters, blood smears, claw scratches, dirt, water stains and grime over the
// INSIDE walls of the generated house. Each decal is a flat see-through picture (a quad) sitting a few
// millimetres in front of the wall: cheap on Quest, no colliders (so nothing bumps into them or
// blocks grabbing), and no shadows.
//
// How to use: open House_Bishal (tell the group chat first: it's a scene edit), then
// Tools > Escape Granny > Wall Grime... Set the amounts and press Generate. Press it again (or change
// the Seed) for a different layout; it always replaces the previous one. Remove deletes them all.
//
// Everything goes under "Wall Grime (generated)", outside "House (generated)". If the house is rebuilt
// (Build House), press Generate again so the decals match the new walls.
// The art comes from WallGrimeTextures (Assets/_Game/Textures/WallGrime/): paint over those PNGs to change it.
//
// How it finds walls: HouseBuilder names each inside wall skin "Wall <h|v><line> [from-to] <Room>"
// (outside skins end in "Outside"), so the name says which way the wall faces.

using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Kind = WallGrimeTextures.Kind;

public class WallGrimeDecorator : EditorWindow
{
    const string HouseRootName = "House (generated)";
    const string GrimeRootName = "Wall Grime (generated)";
    const float EndMargin = 0.15f; // keep away from wall ends, where walls meet at corners and door gaps

    static readonly Regex WallName = new Regex(@"^Wall ([hv])(-?[\d.]+) \[[^\]]*\] (.+)$");

    // What each kind of decal looks like on the wall. Numbers are metres.
    class Spec
    {
        public Kind kind;
        public string group;        // which Amount slider controls it, and the child object it goes under
        public float perSquareMetre; // how many per m² of wall (at Amount 1)
        public float perMetre;       // how many per metre of wall length (for the floor-level grime)
        public float minSize, maxSize; // height of the decal
        public float minHeight, maxHeight; // centre height above the floor
        public float maxTilt;        // random rotation either way, in degrees
        public bool nearCeiling, alongFloor, avoidOverlap;
    }

    static readonly Spec[] Specs =
    {
        new Spec { kind = Kind.BloodSplatter, group = "Blood", perSquareMetre = 0.05f, minSize = 0.4f, maxSize = 0.9f,
                   minHeight = 0.5f, maxHeight = 1.9f, maxTilt = 8f, avoidOverlap = true }, // small tilt: drips must run down
        new Spec { kind = Kind.BloodSmear, group = "Blood", perSquareMetre = 0.02f, minSize = 0.3f, maxSize = 0.55f,
                   minHeight = 0.8f, maxHeight = 1.5f, maxTilt = 15f, avoidOverlap = true },
        new Spec { kind = Kind.Scratches, group = "Scratches", perSquareMetre = 0.05f, minSize = 0.3f, maxSize = 0.6f,
                   minHeight = 0.7f, maxHeight = 1.9f, maxTilt = 25f, avoidOverlap = true },
        new Spec { kind = Kind.Dirt, group = "Dirt", perSquareMetre = 0.1f, minSize = 0.5f, maxSize = 1.3f,
                   minHeight = 0.3f, maxHeight = 2.4f, maxTilt = 180f },
        new Spec { kind = Kind.WaterStain, group = "Dirt", perSquareMetre = 0.025f, minSize = 0.6f, maxSize = 1.1f,
                   nearCeiling = true, maxTilt = 5f },
        new Spec { kind = Kind.Grime, group = "Dirt", perMetre = 0.12f, minSize = 0.3f, maxSize = 0.4f,
                   alongFloor = true },
    };

    [SerializeField] int seed = 4249;
    [SerializeField] float bloodAmount = 1f;
    [SerializeField] float scratchesAmount = 1f;
    [SerializeField] float dirtAmount = 1f;

    [MenuItem("Tools/Escape Granny/Wall Grime...")]
    static void Open() => GetWindow<WallGrimeDecorator>("Wall Grime");

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Scatters blood, scratches and dirt on the inside walls of \"House (generated)\" " +
                                "in the open scene. Generate replaces the previous layout.", MessageType.Info);
        EditorGUILayout.LabelField("Open scene", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        seed = EditorGUILayout.IntField(new GUIContent("Seed", "Change for a different random layout."), seed);
        bloodAmount = EditorGUILayout.Slider(new GUIContent("Blood", "Splatters and smears. 0 = none, 1 = normal."), bloodAmount, 0f, 3f);
        scratchesAmount = EditorGUILayout.Slider(new GUIContent("Scratches", "Claw marks."), scratchesAmount, 0f, 3f);
        dirtAmount = EditorGUILayout.Slider(new GUIContent("Dirt", "Grime patches, water stains, dirt along the floor."), dirtAmount, 0f, 3f);

        EditorGUILayout.Space();
        if (GUILayout.Button("Generate", GUILayout.Height(30))) Generate();
        if (GUILayout.Button("Remove")) Remove(true);
    }

    float Amount(string group) => group == "Blood" ? bloodAmount : group == "Scratches" ? scratchesAmount : dirtAmount;

    // ---------- building ----------

    // One inside face of a wall: where it is, which way it faces, and how big it is.
    struct Face
    {
        public Vector3 normal;     // points into the room
        public Vector3 along;      // horizontal direction along the wall
        public float surface;      // position of the face along the normal axis
        public float from, to;     // extent along the wall
        public float floorY, topY; // bottom and top of the wall
    }

    void Generate()
    {
        var house = GameObject.Find(HouseRootName);
        if (house == null)
        {
            EditorUtility.DisplayDialog("Wall Grime", $"No \"{HouseRootName}\" in the open scene. Open House_Bishal first.", "OK");
            return;
        }

        // The main menu has its own copy of the house as a backdrop, so make sure this is the scene you meant.
        string sceneName = house.scene.name;
        if (sceneName != "House_Bishal" && !EditorUtility.DisplayDialog("Wall Grime",
                $"The open scene is \"{sceneName}\", not House_Bishal (the game). Add wall grime to \"{sceneName}\" anyway?",
                "Add it here", "Cancel"))
            return;

        Remove(false);
        var root = new GameObject(GrimeRootName);
        Undo.RegisterCreatedObjectUndo(root, "Generate Wall Grime");

        var materials = new Dictionary<Kind, List<Material>>();
        foreach (var spec in Specs) materials[spec.kind] = WallGrimeTextures.Materials(spec.kind);
        var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var groups = new Dictionary<string, Transform>();
        var random = new System.Random(seed);
        float R() => (float)random.NextDouble();

        int walls = 0, decals = 0;
        foreach (var renderer in house.GetComponentsInChildren<MeshRenderer>())
        {
            if (!TryGetFace(renderer, out Face face)) continue;
            walls++;

            float length = face.to - face.from - 2f * EndMargin;
            float height = face.topY - face.floorY;
            if (length < 0.3f) continue;

            var placed = new List<Vector3>(); // x = along, y = height, z = radius; for overlap checks
            int layer = 0;                   // each decal sits slightly further out, so overlaps don't flicker

            foreach (var spec in Specs)
            {
                float amount = Amount(spec.group);
                float expected = (spec.perSquareMetre * length * height + spec.perMetre * length) * amount;
                int count = (int)expected + (R() < expected - (int)expected ? 1 : 0);

                for (int n = 0; n < count; n++)
                {
                    float aspect = WallGrimeTextures.Aspect(spec.kind);
                    float h = Mathf.Lerp(spec.minSize, spec.maxSize, R());
                    float w = h * aspect;
                    if (spec.alongFloor) w = Mathf.Min(w * Mathf.Lerp(1f, 1.6f, R()), length);
                    float tilt = (R() * 2f - 1f) * spec.maxTilt;

                    // Room the rotated decal needs, so it never hangs off the wall's end or top.
                    float rad = tilt * Mathf.Deg2Rad;
                    float halfW = 0.5f * (Mathf.Abs(w * Mathf.Cos(rad)) + Mathf.Abs(h * Mathf.Sin(rad)));
                    float halfH = 0.5f * (Mathf.Abs(w * Mathf.Sin(rad)) + Mathf.Abs(h * Mathf.Cos(rad)));
                    if (2f * halfW > length || 2f * halfH > height) continue;

                    bool found = false;
                    float a = 0f, y = 0f;
                    for (int attempt = 0; attempt < 8 && !found; attempt++)
                    {
                        a = Mathf.Lerp(face.from + EndMargin + halfW, face.to - EndMargin - halfW, R());
                        if (spec.alongFloor) y = halfH + 0.01f;
                        else if (spec.nearCeiling) y = height - halfH - 0.02f;
                        else y = Mathf.Clamp(Mathf.Lerp(spec.minHeight, spec.maxHeight, R()), halfH + 0.02f, height - halfH - 0.02f);

                        found = true;
                        if (spec.avoidOverlap)
                            foreach (var p in placed)
                                if (Vector2.Distance(new Vector2(a, y), new Vector2(p.x, p.y)) < (p.z + Mathf.Max(halfW, halfH)) * 0.8f)
                                { found = false; break; }
                    }
                    if (!found) continue;
                    if (spec.avoidOverlap) placed.Add(new Vector3(a, y, Mathf.Max(halfW, halfH)));

                    var mats = materials[spec.kind];
                    var mat = mats[random.Next(mats.Count)];
                    float lift = 0.004f + 0.0008f * (layer++ % 12); // a few mm off the wall
                    Vector3 position = face.normal * (face.surface + lift) + face.along * a + Vector3.up * (face.floorY + y);
                    // Some decals are mirrored, so the same picture doesn't repeat exactly.
                    bool mirror = spec.kind != Kind.BloodSmear && R() < 0.5f;
                    var parent = Group(root.transform, groups, spec.group);
                    MakeDecal(parent, $"{spec.kind} ({renderer.name})", quad, mat, position, face.normal, tilt,
                              new Vector3(mirror ? -w : w, h, 1f));
                    decals++;
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log($"[WallGrimeDecorator] Placed {decals} decals on {walls} inside wall faces (seed {seed}).");
    }

    // Works out the inside face of a HouseBuilder wall skin from its name and bounds.
    static bool TryGetFace(MeshRenderer renderer, out Face face)
    {
        face = default;
        var match = WallName.Match(renderer.name);
        if (!match.Success || match.Groups[3].Value == "Outside") return false;

        char o = match.Groups[1].Value[0];
        float line = float.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
        Bounds b = renderer.bounds;

        // 'h' walls run along x at z = line, 'v' walls run along z at x = line. The skin sits on one
        // side of that line, and that side is the room it faces.
        bool horizontal = o == 'h';
        float centre = horizontal ? b.center.z : b.center.x;
        float side = Mathf.Sign(centre - line);
        Vector3 axis = horizontal ? Vector3.forward : Vector3.right;

        face.normal = axis * side;
        face.along = horizontal ? Vector3.right : Vector3.forward;
        face.surface = side * (side > 0 ? (horizontal ? b.max.z : b.max.x) : (horizontal ? b.min.z : b.min.x));
        face.from = horizontal ? b.min.x : b.min.z;
        face.to = horizontal ? b.max.x : b.max.z;
        face.floorY = b.min.y;
        face.topY = b.max.y;
        return true;
    }

    static void MakeDecal(Transform parent, string name, Mesh quad, Material mat, Vector3 position,
                          Vector3 normal, float tilt, Vector3 scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        // Unity's quad shows its picture on the side facing -Z, so point -Z out of the wall into the room.
        go.transform.SetPositionAndRotation(position,
            Quaternion.AngleAxis(tilt, normal) * Quaternion.LookRotation(-normal, Vector3.up));
        go.transform.localScale = scale;

        go.AddComponent<MeshFilter>().sharedMesh = quad;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = LightProbeUsage.Off;
        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        // Static: Unity merges them at build time, so hundreds of decals cost only a few draw calls.
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
    }

    static Transform Group(Transform root, Dictionary<string, Transform> groups, string name)
    {
        if (groups.TryGetValue(name, out var t)) return t;
        t = new GameObject(name).transform;
        t.SetParent(root, false);
        groups[name] = t;
        return t;
    }

    static void Remove(bool log)
    {
        var old = GameObject.Find(GrimeRootName);
        if (old == null) return;
        var scene = old.scene;
        Undo.DestroyObjectImmediate(old);
        EditorSceneManager.MarkSceneDirty(scene);
        if (log) Debug.Log("[WallGrimeDecorator] Removed wall grime.");
    }
}
