using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Escape Granny > Furnish Living Room
// Places the Free Furniture Set pieces in the ground-floor living room, the same way the team places
// furniture by hand: under Placed Objects (keep) > Furniture > Floor 1 > Living Room (folder at 0,0,0),
// each piece named for what it is. Running it again only replaces the pieces listed below, so anything
// placed in that folder by hand stays.
public static class LivingRoomFurnisher
{
    const string PrefabFolder = "Assets/Free Furniture Set/Prefabs/";
    const string HouseScenePath = "Assets/House_Bishal.unity";
    const string HouseRootName = "House (generated)";
    const string PlacedRootName = "Placed Objects (keep)";
    const float WallGap = 0.03f; // small gap so nothing pokes through a wall

    // Which way the front of a piece points. North = +z (front door side), East = +x.
    enum Facing { North, East, South, West }

    // The models are assumed to face +z. If one ends up backwards or sideways, add its prefab here
    // with the extra turn in degrees (for example 180).
    static readonly Dictionary<string, float> FrontYawOffset = new Dictionary<string, float>
    {
    };

    class Item
    {
        public string name, prefab, floor;
        public float x, z;
        public Facing facing;
        public float? wallX, wallZ; // inner face of a wall the piece is pushed flat against (two = a corner)
        public string onTop;        // name of an earlier piece to stand in (a plant in its pot)

        public Item(string name, string prefab, string floor, float x, float z, Facing facing,
                    float? wallX = null, float? wallZ = null, string onTop = null)
        {
            this.name = name; this.prefab = prefab; this.floor = floor;
            this.x = x; this.z = z; this.facing = facing;
            this.wallX = wallX; this.wallZ = wallZ; this.onTop = onTop;
        }
    }

    // Living Room is x 12-18, z 0-14. Kept clear: the front door (north wall, x 14.4-15.6), the kitchen door
    // and hall opening (west side), the stairs up (south-east corner), the basement door (x 12.4, z 0-1.5)
    // and Granny's spawn G2 (14.5, 3). The east wall in front of the sofa is left empty for the TV (key spot K6).
    const string Main = "Floor - Living Room";
    static readonly Item[] Items =
    {
        // Seating area: sofa facing the east wall (TV goes there), coffee table between, two armchairs
        // with room to walk between them.
        new Item("Living Room Sofa",          "SofaLeatherBlackSmall3_2S", Main, 14.1f, 9.0f,  Facing.East),
        new Item("Living Room Coffee Table",  "CoffeeTableWoodBrown",      Main, 15.6f, 9.0f,  Facing.East),
        new Item("Living Room Armchair 1",    "FotelLeatherBlackSmall_2S", Main, 15.9f, 11.0f, Facing.South),
        new Item("Living Room Armchair 2",    "FotelLeatherBlackSmall_2S", Main, 15.9f, 7.0f,  Facing.North),
        new Item("Living Room Floor Lamp",    "StandingLampS",             Main, 13.3f, 10.5f, Facing.East),

        // A potted plant either side of the front door (north wall), the plant standing in its pot.
        // Kept away from the side walls so the leaves don't poke into the kitchen or outside.
        new Item("Living Room Pot Left",      "BlackPot",                  Main, 13.4f, 13.5f, Facing.South, wallZ: 13.9f),
        new Item("Living Room Plant Left",    "BigEvergreenIvy",           Main, 0f, 0f, Facing.South, onTop: "Living Room Pot Left"),
        new Item("Living Room Pot Right",     "BrownSmallPot",             Main, 16.6f, 13.5f, Facing.South, wallZ: 13.9f),
        new Item("Living Room Plant Right",   "SmallDracaena",             Main, 0f, 0f, Facing.South, onTop: "Living Room Pot Right"),
    };

    // Pieces earlier versions of this tool placed that are no longer wanted; removed on the next run.
    static readonly string[] Removed =
    {
        "Living Room Bookshelf", "Living Room Corner Plant", "Living Room Plant",
        "Living Room Door Pot Left", "Living Room Door Pot Right", "Living Room TV Shelf",
    };

    [MenuItem("Tools/Escape Granny/Furnish Living Room")]
    static void FurnishLivingRoom()
    {
        // Get out of prefab editing and make sure the house scene is the one open.
        StageUtility.GoToMainStage();
        if (SceneManager.GetActiveScene().path != HouseScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(HouseScenePath);
        }

        var house = GameObject.Find(HouseRootName);
        if (house == null)
        {
            EditorUtility.DisplayDialog("Furnish Living Room", "No house found. Run Tools > Escape Granny > Build House first.", "OK");
            return;
        }

        Transform folder = RoomFolder();
        int placed = 0;
        var missing = new List<string>();

        foreach (var name in Removed)
        {
            var old = folder.Find(name);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        }

        foreach (var item in Items)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + item.prefab + ".prefab");
            if (prefab == null) { missing.Add(item.prefab); continue; }

            // Replace this piece if an earlier run made it.
            var old = folder.Find(item.name);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, folder);
            Undo.RegisterCreatedObjectUndo(go, "Furnish Living Room");
            go.name = item.name;

            FrontYawOffset.TryGetValue(item.prefab, out float offset);
            go.transform.rotation = Quaternion.Euler(0f, Yaw(item.facing) + offset, 0f);
            go.transform.position = new Vector3(item.x, 0f, item.z);
            if (item.onTop != null)
            {
                var pot = folder.Find(item.onTop);
                PlaceIn(go, pot);
                KeepInRoom(go, pot);
            }
            else Place(go, item, FloorTop(house.transform, item.floor));
            placed++;
        }

        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        string msg = $"Done: {placed} pieces placed in Placed Objects (keep) > Furniture > Floor 1 > Living Room, and the scene is saved.";
        if (missing.Count > 0) msg += "\n\nCouldn't find: " + string.Join(", ", missing);
        EditorUtility.DisplayDialog("Furnish Living Room", msg, "OK");
    }

    // Placed Objects (keep) > Furniture > Floor 1 > Living Room, made if missing. Folders stay at 0,0,0.
    static Transform RoomFolder()
    {
        Transform t = null;
        foreach (var name in new[] { PlacedRootName, "Furniture", "Floor 1", "Living Room" })
        {
            Transform next = t == null ? GameObject.Find(name)?.transform : t.Find(name);
            if (next == null)
            {
                var go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "Furnish Living Room");
                if (t != null) go.transform.SetParent(t, false);
                next = go.transform;
            }
            t = next;
        }
        return t;
    }

    // Stand the piece on the floor, centred on its spot, and push it flat against any wall it names.
    // The spot says which side of the wall the piece is on.
    static void Place(GameObject go, Item item, float floorY)
    {
        Bounds b = WorldBounds(go);
        go.transform.position += new Vector3(item.x - b.center.x, floorY - b.min.y, item.z - b.center.z);

        b = WorldBounds(go);
        Vector3 shift = Vector3.zero;
        if (item.wallX is float wx)
            shift.x = item.x < wx ? (wx - WallGap) - b.max.x : (wx + WallGap) - b.min.x;
        if (item.wallZ is float wz)
            shift.z = item.z < wz ? (wz - WallGap) - b.max.z : (wz + WallGap) - b.min.z;
        go.transform.position += shift;
    }

    // Stand a plant in its pot: centred on the pot, its base a little below the rim so it looks planted.
    const float PlantSink = 0.45f; // share of the pot's height the plant sits below the rim
    static void PlaceIn(GameObject go, Transform pot)
    {
        if (pot == null) return;
        Bounds p = WorldBounds(pot.gameObject);
        Bounds b = WorldBounds(go);
        float baseY = p.max.y - p.size.y * PlantSink;
        go.transform.position += new Vector3(p.center.x - b.center.x, baseY - b.min.y, p.center.z - b.center.z);
    }

    // Living room inner walls (x 12-18, z 0-14 minus the 0.1 m half wall).
    const float RoomMinX = 12.1f, RoomMaxX = 17.9f, RoomMinZ = 0.1f, RoomMaxZ = 13.9f;

    // If a plant's leaves stick through a wall, slide the plant and its pot back into the room together.
    static void KeepInRoom(GameObject plant, Transform pot)
    {
        Bounds b = WorldBounds(plant);
        Vector3 shift = Vector3.zero;
        if (b.min.x < RoomMinX) shift.x = RoomMinX - b.min.x;
        else if (b.max.x > RoomMaxX) shift.x = RoomMaxX - b.max.x;
        if (b.min.z < RoomMinZ) shift.z = RoomMinZ - b.min.z;
        else if (b.max.z > RoomMaxZ) shift.z = RoomMaxZ - b.max.z;
        plant.transform.position += shift;
        if (pot != null) pot.position += shift;
    }

    static Bounds WorldBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    // Top of the generated floor slab for this room (falls back to y = 0).
    static float FloorTop(Transform house, string floorName)
    {
        float top = float.MinValue;
        foreach (var r in house.GetComponentsInChildren<Renderer>())
            if (r.gameObject.name == floorName) top = Mathf.Max(top, r.bounds.max.y);
        return top == float.MinValue ? 0f : top;
    }

    static float Yaw(Facing f)
    {
        switch (f)
        {
            case Facing.East: return 90f;
            case Facing.South: return 180f;
            case Facing.West: return -90f;
            default: return 0f;
        }
    }
}
