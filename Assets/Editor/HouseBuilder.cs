// HouseBuilder.cs
//
// Builds the Escape the Granny house straight from the floor plan numbers,
// so nobody has to place walls, floors and stairs by hand.
//
// Menu: Tools > Escape Granny >
//   Build House            builds (or rebuilds) all three floors
//   Remove House           deletes the generated house
//   Apply Horror Lighting  dark ambient, fog, moonlight and one dim lamp per room
//
// Put this file in Assets/Editor/ (the folder name "Editor" matters).
// FlickerLight.cs must live OUTSIDE any Editor folder (e.g. Assets/Scripts/).
//
// All numbers are meters and match the floor plan PDF:
//   origin (0, 0) = south-west outer corner, +x east, +z north, +y up.
//   basement floor y = -3, ground floor y = 0, upper floor y = +3, roof y = +6.
//
// Look and feel:
//   Every room has a "theme" (bedroom, bath, hallway...). Each theme picks a wall
//   and floor material from Assets/Generated/HouseMaterials. Drop a texture into a
//   material's Base Map and every room using it updates. UVs are in meters, so set
//   a material's Tiling to 1/size of the texture in meters (2 m texture -> 0.5).
//
// Rebuilding:
//   Anything a teammate placed inside "House (generated)" (furniture, props) is moved
//   to "Placed Objects (keep)" before the old house is deleted, so it is never lost.

using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class HouseBuilder
{
    // ------------------------------------------------------------------
    // Building constants
    // ------------------------------------------------------------------
    const string RootName = "House (generated)";
    const string LightingRootName = "House Lighting (generated)";
    const string PlacedRootName = "Placed Objects (keep)";
    const string MaterialFolder = "Assets/Generated/HouseMaterials";

    const float WallThickness = 0.2f;
    const float SlabThickness = 0.2f;
    const float FloorToFloor = 3f;
    const float WallHeight = FloorToFloor - SlabThickness; // 2.8 m clear, the slab above sits on top
    const float DoorHeight = 2.2f;
    const float RailHeight = 1f;
    const float RailThickness = 0.06f;
    const float RoofY = 6f;

    // Every flight: 16 risers of 0.1875 m. We model 15 steps; the 16th "step" is the floor above.
    const int StepCount = 15;
    const float Tread = 0.28f;
    const float Rise = FloorToFloor / (StepCount + 1);
    const float Run = StepCount * Tread; // 4.2 m

    // ------------------------------------------------------------------
    // Materials and room themes
    // ------------------------------------------------------------------

    // Material name -> starting color. The colors are already dark and dirty so the house
    // reads as old even before anyone adds textures. Existing materials are never overwritten,
    // so textures a teammate assigns survive every rebuild.
    static readonly Dictionary<string, Color> MaterialColors = new Dictionary<string, Color>
    {
        { "Wall - Faded Wallpaper",    new Color(0.36f, 0.33f, 0.27f) },
        { "Wall - Nursery Wallpaper",  new Color(0.38f, 0.33f, 0.32f) },
        { "Wall - Wood Panel",         new Color(0.25f, 0.17f, 0.11f) },
        { "Wall - Dirty Tile",         new Color(0.42f, 0.43f, 0.40f) },
        { "Wall - Stained Plaster",    new Color(0.33f, 0.31f, 0.28f) },
        { "Wall - Basement Concrete",  new Color(0.25f, 0.25f, 0.24f) },
        { "Wall - Exterior Siding",    new Color(0.22f, 0.21f, 0.20f) },
        { "Floor - Dark Wood",         new Color(0.20f, 0.14f, 0.09f) },
        { "Floor - Dirty Tile",        new Color(0.33f, 0.32f, 0.30f) },
        { "Floor - Rough Planks",      new Color(0.24f, 0.19f, 0.13f) },
        { "Floor - Concrete",          new Color(0.22f, 0.22f, 0.21f) },
        { "Ceiling - Stained Plaster", new Color(0.30f, 0.29f, 0.26f) },
        { "Roof - Old Shingles",       new Color(0.12f, 0.11f, 0.11f) },
        { "Stairs - Worn Wood",        new Color(0.22f, 0.15f, 0.10f) },
        { "Rail - Dark Wood",          new Color(0.15f, 0.10f, 0.07f) },
    };

    const string ExteriorWall = "Wall - Exterior Siding";
    const string CeilingMat = "Ceiling - Stained Plaster";
    const string RoofMat = "Roof - Old Shingles";
    const string StairsMat = "Stairs - Worn Wood";
    const string RailMat = "Rail - Dark Wood";

    // Theme -> (wall material, floor material). Change a room's look by changing its theme below.
    static readonly Dictionary<string, string[]> Themes = new Dictionary<string, string[]>
    {
        { "bedroom",  new[] { "Wall - Faded Wallpaper",   "Floor - Dark Wood" } },
        { "nursery",  new[] { "Wall - Nursery Wallpaper", "Floor - Dark Wood" } },
        { "living",   new[] { "Wall - Faded Wallpaper",   "Floor - Dark Wood" } },
        { "kitchen",  new[] { "Wall - Dirty Tile",        "Floor - Dirty Tile" } },
        { "bath",     new[] { "Wall - Dirty Tile",        "Floor - Dirty Tile" } },
        { "hallway",  new[] { "Wall - Wood Panel",        "Floor - Dark Wood" } },
        { "closet",   new[] { "Wall - Stained Plaster",   "Floor - Rough Planks" } },
        { "storage",  new[] { "Wall - Stained Plaster",   "Floor - Rough Planks" } },
        { "basement", new[] { "Wall - Basement Concrete", "Floor - Concrete" } },
    };

    // ------------------------------------------------------------------
    // Small data types
    // ------------------------------------------------------------------

    // A wall on a straight grid line. 'h' walls run along x at z = line, 'v' walls run along z at x = line.
    class Wall
    {
        public char o; public float line, from, to;
        public Wall(char o, float line, float from, float to) { this.o = o; this.line = line; this.from = from; this.to = to; }
    }

    // An opening cut out of a wall. Doors get a door slot marker; open gaps (archways) do not.
    class Gap
    {
        public string id; public char o; public float line, from, to; public bool isDoor;
        public Gap(string id, char o, float line, float from, float to, bool isDoor = true)
        { this.id = id; this.o = o; this.line = line; this.from = from; this.to = to; this.isDoor = isDoor; }
    }

    class Rect2
    {
        public float x0, z0, x1, z1;
        public Rect2(float x0, float z0, float x1, float z1) { this.x0 = x0; this.z0 = z0; this.x1 = x1; this.z1 = z1; }
        public bool Contains(float x, float z) { return x >= x0 && x <= x1 && z >= z0 && z <= z1; }
    }

    // A room: its floor area and which theme (materials) it uses.
    class Room
    {
        public string name, theme; public Rect2 r;
        public Room(string name, string theme, float x0, float z0, float x1, float z1)
        { this.name = name; this.theme = theme; r = new Rect2(x0, z0, x1, z1); }
    }

    // A straight flight. axis = direction you walk going UP ('x' or 'z').
    // acrossFrom/acrossTo = the flight's width on the other axis, start = where the bottom step begins.
    class Stair
    {
        public string name; public char axis; public float acrossFrom, acrossTo, start, bottomY;
        public Stair(string name, char axis, float acrossFrom, float acrossTo, float start, float bottomY)
        { this.name = name; this.axis = axis; this.acrossFrom = acrossFrom; this.acrossTo = acrossTo; this.start = start; this.bottomY = bottomY; }
    }

    class Marker
    {
        public string id, note; public float x, z;
        public Marker(string id, float x, float z, string note) { this.id = id; this.x = x; this.z = z; this.note = note; }
    }

    class Level
    {
        public string name; public float y;
        public List<Room> rooms = new List<Room>();
        public List<Rect2> floorHoles = new List<Rect2>();
        public List<Wall> walls = new List<Wall>();
        public List<Gap> gaps = new List<Gap>();
        public List<Wall> rails = new List<Wall>();
        public List<Marker> markers = new List<Marker>();
        public bool IsTop { get { return Mathf.Abs(y + FloorToFloor - RoofY) < 0.001f; } }

        public Room RoomAt(float x, float z)
        {
            foreach (var room in rooms) if (room.r.Contains(x, z)) return room;
            return null; // outside the house
        }
    }

    // ------------------------------------------------------------------
    // Floor plan data (same numbers as the PDF)
    // ------------------------------------------------------------------
    static List<Level> BuildPlan()
    {
        var levels = new List<Level>();

        // ---------------- Upper floor, y = +3 ----------------
        var up = new Level { name = "Upper Floor", y = 3f };
        up.rooms.AddRange(new[] {
            new Room("Master Bedroom", "bedroom", 0, 8, 5, 14),
            new Room("Bathroom",       "bath",    0, 5, 3, 8),
            new Room("Guest Bedroom",  "bedroom", 0, 0, 5, 5),
            new Room("Wardrobe Room",  "closet",  5, 0, 8, 5),
            new Room("Nursery",        "nursery", 8, 8, 12, 14),
            new Room("Storage Room",   "storage", 12, 8, 18, 14),
            new Room("Sewing Room",    "bedroom", 8, 0, 16.5f, 5),
            new Room("Closet",         "closet",  16.5f, 0, 18, 1.5f),
            new Room("Hallway",        "hallway", 3, 5, 18, 8),
            new Room("Gallery",        "hallway", 5, 8, 8, 14),
            new Room("Stairwell C",    "hallway", 16.5f, 1.5f, 18, 5),
        });
        up.floorHoles.Add(new Rect2(5, 8.6f, 6.5f, 12.8f));    // over stairs A
        up.floorHoles.Add(new Rect2(16.5f, 1.5f, 18, 5.7f));   // over stairs C
        up.walls.AddRange(new[] {
            new Wall('h', 0, 0, 18), new Wall('h', 14, 0, 18), new Wall('v', 0, 0, 14), new Wall('v', 18, 0, 14),
            new Wall('h', 8, 0, 5), new Wall('h', 8, 8, 18), new Wall('h', 5, 0, 16.5f),
            new Wall('v', 3, 5, 8), new Wall('v', 5, 8, 14), new Wall('v', 8, 8, 14), new Wall('v', 12, 8, 14),
            new Wall('v', 5, 0, 5), new Wall('v', 8, 0, 5), new Wall('v', 16.5f, 0, 5), new Wall('h', 1.5f, 16.5f, 18),
        });
        up.gaps.AddRange(new[] {
            new Gap("U1_MasterBedroom", 'h', 8, 3.4f, 4.6f),
            new Gap("U2_Bathroom", 'v', 3, 6.4f, 7.6f),
            new Gap("U3_GuestBedroom", 'h', 5, 3.4f, 4.6f),
            new Gap("U4_WardrobeRoom", 'v', 5, 1.0f, 2.2f),
            new Gap("U5_Nursery", 'h', 8, 9.4f, 10.6f),
            new Gap("U6_NurseryGallery", 'v', 8, 11.0f, 12.2f),
            new Gap("U7_Storage", 'h', 8, 13.4f, 14.6f),
            new Gap("U8_NurseryStorage", 'v', 12, 10.4f, 11.6f),
            new Gap("U9_SewingRoomWest", 'h', 5, 9.4f, 10.6f),
            new Gap("U10_SewingRoomEast", 'h', 5, 14.0f, 15.2f),
            new Gap("U11_SewingCloset", 'v', 16.5f, 0.3f, 1.2f),
        });
        up.rails.AddRange(new[] {
            new Wall('h', 8.6f, 5, 6.5f), new Wall('v', 6.5f, 8.6f, 12.8f), new Wall('v', 16.5f, 5, 5.7f),
        });
        up.markers.AddRange(new[] {
            new Marker("P1", 2.5f, 12.0f, "Prisoner spawn, master bedroom"),
            new Marker("P2", 2.5f, 3.4f, "Prisoner spawn, guest bedroom"),
            new Marker("P3", 15.5f, 10.3f, "Prisoner spawn, storage room"),
            new Marker("K10", 0.7f, 12.8f, "Key: master nightstand drawer"),
            new Marker("K11", 2.4f, 5.5f, "Key: bathroom medicine cabinet"),
            new Marker("K12", 4.3f, 0.7f, "Key: guest desk drawer"),
            new Marker("K13", 11.3f, 9.0f, "Key: nursery toy chest"),
            new Marker("K14", 17.2f, 13.2f, "Key: storage crate"),
            new Marker("K15", 8.8f, 0.7f, "Key: sewing room dresser"),
            new Marker("H7", 4.3f, 13.3f, "Hide: master wardrobe"),
            new Marker("H8", 1.2f, 9.3f, "Hide: under master bed"),
            new Marker("H9", 0.6f, 7.3f, "Hide: bathtub"),
            new Marker("H10", 6.5f, 1.2f, "Hide: wardrobe room coats"),
            new Marker("H11", 8.7f, 13.3f, "Hide: behind nursery cabinet"),
            new Marker("H12", 13.0f, 13.2f, "Hide: storage box maze"),
            new Marker("H13", 15.8f, 0.7f, "Hide: sewing room wardrobe"),
            new Marker("H14", 17.25f, 0.75f, "Hide: closet behind U11"),
        });
        levels.Add(up);

        // ---------------- Ground floor, y = 0 ----------------
        var gr = new Level { name = "Ground Floor", y = 0f };
        gr.rooms.AddRange(new[] {
            new Room("Living Room",        "living",  12, 0, 18, 14),
            new Room("Living Room (L)",    "living",  8, 0, 12, 5),
            new Room("Kitchen",            "kitchen", 8, 8, 12, 14),
            new Room("Bedroom 1",          "bedroom", 0, 8, 5, 14),
            new Room("Bedroom 2",          "bedroom", 0, 0, 5, 5),
            new Room("Shared Bath",        "bath",    0, 5, 3, 8),
            new Room("Common Bath",        "bath",    5, 0, 8, 5),
            new Room("Hallway",            "hallway", 3, 5, 12, 8),
            new Room("Stairs A",           "hallway", 5, 8, 6.5f, 12.8f),
            new Room("Under-stair Closet", "closet",  5, 12.8f, 6.5f, 14),
            new Room("Linen Closet",       "closet",  6.5f, 8, 8, 14),
        });
        gr.floorHoles.Add(new Rect2(8.0f, 0, 12.4f, 1.5f));    // over basement stairs B
        gr.walls.AddRange(new[] {
            new Wall('h', 0, 0, 18), new Wall('h', 14, 0, 18), new Wall('v', 0, 0, 14), new Wall('v', 18, 0, 14),
            new Wall('h', 8, 0, 12), new Wall('h', 5, 0, 12),
            new Wall('v', 12, 8, 14), new Wall('v', 5, 8, 14), new Wall('v', 6.5f, 8, 14), new Wall('v', 8, 8, 14),
            new Wall('v', 3, 5, 8), new Wall('v', 5, 0, 5), new Wall('v', 8, 0, 5),
            new Wall('h', 12.8f, 5, 6.5f),                                   // back of the under-stair closet
            new Wall('v', 16.5f, 1.5f, 5.7f), new Wall('h', 5.7f, 16.5f, 18), // stairs C enclosure
            new Wall('h', 1.5f, 8.2f, 12.4f), new Wall('v', 12.4f, 0, 1.5f),  // stairs B enclosure
        });
        gr.gaps.AddRange(new[] {
            new Gap("D1_FrontDoor", 'h', 14, 14.4f, 15.6f),
            new Gap("D2_Bedroom1", 'h', 8, 3.4f, 4.6f),
            new Gap("D3_Bedroom1Bath", 'h', 8, 0.9f, 2.1f),
            new Gap("D4_Bedroom2Bath", 'h', 5, 0.9f, 2.1f),
            new Gap("D5_Bedroom2", 'h', 5, 3.4f, 4.6f),
            new Gap("D6_CommonBath", 'h', 5, 5.9f, 7.1f),
            new Gap("D7_KitchenHall", 'h', 8, 9.4f, 10.6f),
            new Gap("D8_KitchenLiving", 'v', 12, 10.4f, 11.6f),
            new Gap("D9_LinenCloset", 'h', 8, 6.8f, 7.7f),
            new Gap("D10_UnderStairCloset", 'v', 5, 13.0f, 13.9f),
            new Gap("D11_BasementDoor", 'v', 12.4f, 0, 1.5f),
            new Gap("O1_StairsA", 'h', 8, 5.0f, 6.5f, false),
        });
        gr.markers.AddRange(new[] {
            new Marker("G1", 10.0f, 11.5f, "Granny spawn, kitchen"),
            new Marker("G2", 14.5f, 3.0f, "Granny spawn, living room"),
            new Marker("K1", 3.2f, 13.3f, "Key: bedroom 1 dresser"),
            new Marker("K2", 2.5f, 5.5f, "Key: shared bath mirror cabinet"),
            new Marker("K3", 0.6f, 3.2f, "Key: bedroom 2 nightstand"),
            new Marker("K4", 7.3f, 1.0f, "Key: common bath sink cabinet"),
            new Marker("K5", 11.3f, 9.0f, "Key: kitchen counter drawer"),
            new Marker("K6", 17.3f, 9.0f, "Key: living TV cabinet"),
            new Marker("K7", 10.0f, 4.4f, "Key: living bookshelf drawer"),
            new Marker("H1", 5.75f, 13.4f, "Hide: under-stair closet"),
            new Marker("H2", 1.3f, 12.4f, "Hide: under bedroom 1 bed"),
            new Marker("H3", 4.4f, 0.8f, "Hide: bedroom 2 wardrobe"),
            new Marker("H4", 8.6f, 13.3f, "Hide: kitchen pantry"),
            new Marker("H5", 7.25f, 12.6f, "Hide: linen closet"),
        });
        levels.Add(gr);

        // ---------------- Basement, y = -3 ----------------
        var bs = new Level { name = "Basement", y = -3f };
        bs.rooms.Add(new Room("Basement", "basement", 4, 0, 14, 7));
        bs.walls.AddRange(new[] {
            new Wall('h', 0, 4, 14), new Wall('h', 7, 4, 14), new Wall('v', 4, 0, 7), new Wall('v', 14, 0, 7),
            new Wall('h', 1.5f, 8.2f, 12.4f), new Wall('v', 12.4f, 0, 1.5f),  // side and high end of stairs B
        });
        bs.markers.AddRange(new[] {
            new Marker("G3", 5.5f, 5.5f, "Granny spawn, basement"),
            new Marker("K8", 4.6f, 6.3f, "Key: workbench drawer"),
            new Marker("K9", 13.4f, 6.3f, "Key: toolbox on shelf"),
            new Marker("H6", 13.2f, 3.2f, "Hide: behind boiler"),
        });
        levels.Add(bs);

        return levels;
    }

    static readonly Stair[] Stairs = {
        new Stair("Stairs A (main, ground to upper)", 'z', 5f, 6.5f, 8.6f, 0f),
        new Stair("Stairs C (back, ground to upper)", 'z', 16.5f, 18f, 1.5f, 0f),
        new Stair("Stairs B (basement to ground)", 'x', 0f, 1.5f, 8.2f, -3f),
    };

    static readonly Rect2 RoofFootprint = new Rect2(0, 0, 18, 14);

    // Escape trigger just outside the front door (x 13.5 to 16.5, z 14.3 to 15.4)
    static readonly Rect2 EscapeZone = new Rect2(13.5f, 14.3f, 16.5f, 15.4f);

    // Rooms that get a flickering lamp, and rooms that stay extra dark
    static readonly HashSet<string> FlickerRooms = new HashSet<string> { "Ground Floor/Hallway", "Basement/Basement", "Upper Floor/Nursery", "Upper Floor/Storage Room" };
    static readonly HashSet<string> NoLampRooms = new HashSet<string> { "Ground Floor/Living Room (L)", "Ground Floor/Stairs A", "Upper Floor/Stairwell C" };

    // ------------------------------------------------------------------
    // Menu commands
    // ------------------------------------------------------------------
    [MenuItem("Tools/Escape Granny/Build House")]
    public static void BuildHouse()
    {
        // Save anything teammates placed inside the old house before deleting it
        int rescued = 0;
        var old = GameObject.Find(RootName);
        if (old != null) rescued = RescuePlacedObjects(old);
        RemoveHouse();

        var mats = new Materials();
        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build House");

        int pieces = 0, markers = 0;
        foreach (var level in BuildPlan())
        {
            var levelGo = Child(root.transform, level.name);
            var structure = Child(levelGo, "Structure");
            var doors = Child(levelGo, "Door Slots");
            var gameplay = Child(levelGo, "Gameplay Markers");

            // Floor: one slab per room (so each room gets its own floor material), with stair holes cut out.
            // Top half is the floor of this level, bottom half is the ceiling of the level below.
            var floorParent = Child(structure, "Floor");
            foreach (var room in level.rooms)
            {
                foreach (var r in SubtractAll(room.r, level.floorHoles))
                {
                    Slab(floorParent, "Floor - " + room.name, r, level.y, mats.Get(Themes[room.theme][1]));
                    Slab(floorParent, "Ceiling - " + room.name, r, level.y - SlabThickness / 2f, mats.Get(CeilingMat));
                    pieces += 2;
                }
            }

            // Walls, with door and archway gaps cut out, plus a lintel above each gap
            var wallParent = Child(structure, "Walls");
            foreach (var w in level.walls) pieces += BuildWall(wallParent, w, level, mats);
            foreach (var g in level.gaps)
            {
                pieces += Lintel(wallParent, g, level, mats);
                if (g.isDoor) { DoorSlot(doors, g, level.y); markers++; }
            }

            // Railings around stair openings
            if (level.rails.Count > 0)
            {
                var railParent = Child(structure, "Railings");
                foreach (var r in level.rails) { Railing(railParent, r, level.y, mats.Get(RailMat)); pieces++; }
            }

            // Spawn, key and hiding spot markers
            foreach (var m in level.markers) { GameplayMarker(gameplay, m, level.y); markers++; }
        }

        // Roof: shingles on top, ceiling of the upper floor underneath
        var roof = Child(root.transform, "Roof");
        Slab(roof, "Roof - Top", RoofFootprint, RoofY, mats.Get(RoofMat));
        Slab(roof, "Ceiling - Upper Floor", RoofFootprint, RoofY - SlabThickness / 2f, mats.Get(CeilingMat));
        pieces += 2;

        // Staircases
        var stairsParent = Child(root.transform, "Stairs");
        foreach (var s in Stairs) { BuildStair(stairsParent, s, mats.Get(StairsMat)); pieces += StepCount + 1; }

        // Escape trigger outside the front door
        var escape = new GameObject("EscapeTrigger");
        escape.transform.SetParent(root.transform, false);
        escape.transform.position = new Vector3((EscapeZone.x0 + EscapeZone.x1) / 2f, 1f, (EscapeZone.z0 + EscapeZone.z1) / 2f);
        var trigger = escape.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(EscapeZone.x1 - EscapeZone.x0, 2f, EscapeZone.z1 - EscapeZone.z0);
        SetIcon(escape, 6);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = root;
        string rescueNote = rescued > 0 ? $" Moved {rescued} placed object(s) to \"{PlacedRootName}\"." : "";
        Debug.Log($"[HouseBuilder] Built house: {pieces} structure pieces, {markers} markers.{rescueNote}");
    }

    [MenuItem("Tools/Escape Granny/Remove House")]
    public static void RemoveHouse()
    {
        var old = GameObject.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old);
    }

    [MenuItem("Tools/Escape Granny/Apply Horror Lighting")]
    public static void ApplyHorrorLighting()
    {
        // Thick fog and almost no ambient light: rooms fade into haze a few meters away.
        // The fog is a dim blue-grey, not black: black fog over a dark room just looks like darkness.
        // Density 0.18 (squared) hides about half of what is 5 m away and almost all of it at 8 m.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.08f, 0.09f, 0.11f);
        RenderSettings.fogDensity = 0.18f;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.035f, 0.035f, 0.045f);

        // Turn any sun into weak, cold moonlight
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type != LightType.Directional) continue;
            Undo.RecordObject(l, "Horror Lighting");
            l.intensity = 0.08f;
            l.color = new Color(0.55f, 0.62f, 0.8f);
        }

        // One dim, warm bulb near the ceiling of each room
        var old = GameObject.Find(LightingRootName);
        if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject(LightingRootName);
        Undo.RegisterCreatedObjectUndo(root, "Horror Lighting");

        int lamps = 0;
        foreach (var level in BuildPlan())
        {
            foreach (var room in level.rooms)
            {
                string key = level.name + "/" + room.name;
                if (NoLampRooms.Contains(key)) continue;

                float w = room.r.x1 - room.r.x0, d = room.r.z1 - room.r.z0;
                bool small = room.theme == "closet";
                bool basement = room.theme == "basement";

                var go = new GameObject("Lamp - " + key);
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3((room.r.x0 + room.r.x1) / 2f, level.y + 2.5f, (room.r.z0 + room.r.z1) / 2f);

                var lamp = go.AddComponent<Light>();
                lamp.type = LightType.Point;
                lamp.shadows = LightShadows.None;   // shadows are expensive on Quest
                lamp.range = small ? 2.5f : Mathf.Clamp(Mathf.Max(w, d) * 0.75f, 3f, 7f);
                lamp.intensity = small ? 0.4f : basement ? 0.6f : 0.9f;
                lamp.color = basement ? new Color(0.7f, 0.85f, 0.6f) : new Color(1f, 0.72f, 0.42f);

                if (FlickerRooms.Contains(key))
                {
                    var flicker = go.AddComponent<FlickerLight>();
                    flicker.maxIntensity = lamp.intensity;
                    flicker.minIntensity = lamp.intensity * 0.3f;
                }
                lamps++;
            }
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[HouseBuilder] Horror lighting applied: fog on, moonlight, {lamps} room lamps ({FlickerRooms.Count} flicker).");
    }

    // ------------------------------------------------------------------
    // Keeping teammates' objects safe on rebuild
    // ------------------------------------------------------------------

    // Names the builder itself creates. Anything else found inside the house was placed by a person.
    static readonly Regex GeneratedName = new Regex(
        @"^(Upper Floor|Ground Floor|Basement|Roof|Stairs|Structure|Door Slots|Gameplay Markers|Floor|Walls|Railings|Railing|" +
        @"Prisoner Spawns|Granny Spawns|Key Spots|Hiding Spots|EscapeTrigger|Ramp Collider \(invisible\)|Step \d+|" +
        @"(Wall|Lintel|Floor|Ceiling|Roof|Stairs) .*|[PGKH]\d+ - .*|[DU]\d+_.*)$");

    static int RescuePlacedObjects(GameObject oldRoot)
    {
        var found = new List<Transform>();
        CollectPlaced(oldRoot.transform, found);
        if (found.Count == 0) return 0;

        var keep = GameObject.Find(PlacedRootName);
        if (keep == null)
        {
            keep = new GameObject(PlacedRootName);
            Undo.RegisterCreatedObjectUndo(keep, "Keep placed objects");
        }
        // Reparenting keeps each object's world position, so nothing moves visually
        foreach (var t in found) Undo.SetTransformParent(t, keep.transform, "Keep placed objects");
        return found.Count;
    }

    static void CollectPlaced(Transform parent, List<Transform> found)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (GeneratedName.IsMatch(c.name)) CollectPlaced(c, found);
            else found.Add(c);
        }
    }

    // ------------------------------------------------------------------
    // Geometry helpers
    // ------------------------------------------------------------------

    // A wall is built as two half-thickness skins, one per side, so each side can show the
    // material of the room it faces. Pieces are split around door gaps and at room edges.
    static int BuildWall(Transform parent, Wall w, Level level, Materials mats)
    {
        var cuts = new List<Gap>();
        foreach (var g in level.gaps)
            if (g.o == w.o && Mathf.Abs(g.line - w.line) < 0.001f && g.to > w.from && g.from < w.to) cuts.Add(g);
        cuts.Sort((p, q) => p.from.CompareTo(q.from));

        // Solid pieces between gaps. A piece only gets the half-thickness extension at a real
        // wall end (so corners close), never at the edge of a door gap.
        var pieces = new List<float[]>(); // from, to, extendFrom(0/1), extendTo(0/1)
        float start = w.from;
        bool startIsWallEnd = true;
        foreach (var g in cuts)
        {
            if (g.from > start) pieces.Add(new[] { start, g.from, startIsWallEnd ? 1f : 0f, 0f });
            start = g.to;
            startIsWallEnd = false;
        }
        if (start < w.to) pieces.Add(new[] { start, w.to, startIsWallEnd ? 1f : 0f, 1f });

        int count = 0;
        foreach (var p in pieces)
        {
            var spans = SplitAtRoomEdges(p[0], p[1], w.o, level);
            for (int i = 0; i < spans.Count - 1; i++)
            {
                bool extA = i == 0 && p[2] > 0f;
                bool extB = i == spans.Count - 2 && p[3] > 0f;
                count += WallSkins(parent, w.o, w.line, spans[i], spans[i + 1], extA, extB, 0f, WallHeight, level, mats, "Wall");
            }
        }
        return count;
    }

    // Break points along a wall wherever a room boundary crosses it, so each skin faces one room
    static List<float> SplitAtRoomEdges(float a, float b, char o, Level level)
    {
        var pts = new List<float> { a, b };
        foreach (var room in level.rooms)
        {
            float e0 = o == 'h' ? room.r.x0 : room.r.z0;
            float e1 = o == 'h' ? room.r.x1 : room.r.z1;
            if (e0 > a + 0.001f && e0 < b - 0.001f && !pts.Contains(e0)) pts.Add(e0);
            if (e1 > a + 0.001f && e1 < b - 0.001f && !pts.Contains(e1)) pts.Add(e1);
        }
        pts.Sort();
        return pts;
    }

    // Builds the two skins of one wall span between heights y0..y1 (relative to the level floor)
    static int WallSkins(Transform parent, char o, float line, float a, float b, bool extA, bool extB,
                         float y0, float y1, Level level, Materials mats, string label)
    {
        float half = WallThickness / 2f;
        float lo = a - (extA ? half : 0f);
        float hi = b + (extB ? half : 0f);
        float mid = (a + b) / 2f, len = hi - lo, along = (lo + hi) / 2f;

        foreach (int side in new[] { -1, 1 })
        {
            // Look 0.3 m to this side of the wall to see which room (if any) it faces
            float sx = o == 'h' ? mid : line + side * 0.3f;
            float sz = o == 'h' ? line + side * 0.3f : mid;
            var room = level.RoomAt(sx, sz);
            bool exterior = room == null;
            string matName = exterior ? ExteriorWall : Themes[room.theme][0];

            // Outside skins also cover the slab edges between floors (and the roof edge on top),
            // so the facade is one clean surface with no ledges
            float bottom = level.y + y0 - (exterior && y0 == 0f ? SlabThickness : 0f);
            float top = level.y + y1 + (exterior && level.IsTop && y1 == WallHeight ? SlabThickness : 0f);

            float offset = side * half / 2f;
            var center = o == 'h'
                ? new Vector3(along, (bottom + top) / 2f, line + offset)
                : new Vector3(line + offset, (bottom + top) / 2f, along);
            var size = o == 'h'
                ? new Vector3(len, top - bottom, half)
                : new Vector3(half, top - bottom, len);
            string facing = exterior ? "Outside" : room.name;
            Box(parent, $"{label} {o}{line} [{a}-{b}] {facing}", center, size, mats.Get(matName));
        }
        return 2;
    }

    // Wall piece above a door or archway, from door height up to the ceiling
    static int Lintel(Transform parent, Gap g, Level level, Materials mats)
    {
        return WallSkins(parent, g.o, g.line, g.from, g.to, false, false, DoorHeight, WallHeight, level, mats, "Lintel " + g.id);
    }

    static void Railing(Transform parent, Wall r, float levelY, Material mat)
    {
        float mid = (r.from + r.to) / 2f, len = r.to - r.from;
        var center = r.o == 'h'
            ? new Vector3(mid, levelY + RailHeight / 2f, r.line)
            : new Vector3(r.line, levelY + RailHeight / 2f, mid);
        var size = r.o == 'h' ? new Vector3(len, RailHeight, RailThickness) : new Vector3(RailThickness, RailHeight, len);
        Box(parent, "Railing", center, size, mat);
    }

    // Half-thickness slab layer whose top surface sits exactly at topY
    static void Slab(Transform parent, string name, Rect2 r, float topY, Material mat)
    {
        float t = SlabThickness / 2f;
        var center = new Vector3((r.x0 + r.x1) / 2f, topY - t / 2f, (r.z0 + r.z1) / 2f);
        var size = new Vector3(r.x1 - r.x0, t, r.z1 - r.z0);
        Box(parent, name, center, size, mat);
    }

    // One flight = 15 visible steps (no colliders) + one invisible ramp collider.
    // Walking over step colliders in VR is bumpy and makes people sick, so the ramp does the work.
    static void BuildStair(Transform parent, Stair s, Material mat)
    {
        var flight = Child(parent, s.name);
        float acrossMid = (s.acrossFrom + s.acrossTo) / 2f;
        float width = s.acrossTo - s.acrossFrom;

        // Turns (distance along the run, height) into a world position
        System.Func<float, float, Vector3> P = (along, y) =>
            s.axis == 'z' ? new Vector3(acrossMid, y, along) : new Vector3(along, y, acrossMid);

        for (int i = 0; i < StepCount; i++)
        {
            float top = (i + 1) * Rise;
            var center = P(s.start + i * Tread + Tread / 2f, s.bottomY + top / 2f);
            var size = s.axis == 'z' ? new Vector3(width, top, Tread) : new Vector3(Tread, top, width);
            var step = Box(flight, $"Step {i + 1}", center, size, mat);
            Object.DestroyImmediate(step.GetComponent<Collider>());
        }

        // Ramp from the bottom edge to the edge of the floor above; its top face passes through both points.
        // The bottom end is pushed 0.3 m further out and below the floor so there is no lip to trip on.
        const float thickness = 0.1f;
        Vector3 bottom = P(s.start, s.bottomY);
        Vector3 topEdge = P(s.start + Run, s.bottomY + FloorToFloor);
        Vector3 dir = (topEdge - bottom).normalized;
        bottom -= dir * 0.3f;
        var rot = Quaternion.LookRotation(dir, Vector3.up);
        Vector3 normal = rot * Vector3.up;
        var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ramp.name = "Ramp Collider (invisible)";
        ramp.transform.SetParent(flight, false);
        ramp.transform.position = (bottom + topEdge) / 2f - normal * (thickness / 2f);
        ramp.transform.rotation = rot;
        ramp.transform.localScale = new Vector3(width, thickness, Vector3.Distance(bottom, topEdge));
        ramp.GetComponent<MeshRenderer>().enabled = false;
        ramp.isStatic = true;
    }

    // Empty object at each door opening, facing along the wall, so the interaction
    // teammate can drop a door prefab on it. Scale x = door width.
    static void DoorSlot(Transform parent, Gap g, float levelY)
    {
        var go = new GameObject(g.id);
        go.transform.SetParent(parent, false);
        float mid = (g.from + g.to) / 2f;
        go.transform.position = g.o == 'h' ? new Vector3(mid, levelY, g.line) : new Vector3(g.line, levelY, mid);
        go.transform.rotation = g.o == 'h' ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
        go.transform.localScale = new Vector3(g.to - g.from, 1f, 1f);
        SetIcon(go, 1);
    }

    static void GameplayMarker(Transform parent, Marker m, float levelY)
    {
        string group = m.id[0] == 'P' ? "Prisoner Spawns" : m.id[0] == 'G' ? "Granny Spawns" : m.id[0] == 'K' ? "Key Spots" : "Hiding Spots";
        var groupT = parent.Find(group);
        if (groupT == null) groupT = Child(parent, group);

        var go = new GameObject($"{m.id} - {m.note}");
        go.transform.SetParent(groupT, false);
        go.transform.position = new Vector3(m.x, levelY, m.z);
        // label colors: green = prisoner, red = granny, yellow = key, purple = hiding
        int color = m.id[0] == 'P' ? 3 : m.id[0] == 'G' ? 6 : m.id[0] == 'K' ? 4 : 7;
        SetIcon(go, color);
    }

    // ------------------------------------------------------------------
    // Low-level helpers
    // ------------------------------------------------------------------

    // A box with a BoxCollider whose UVs are in world meters. That keeps textures the same size
    // on every wall no matter how long it is, and lines patterns up across neighbouring pieces.
    static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); // comes with a BoxCollider
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.transform.localScale = size;
        go.GetComponent<MeshFilter>().sharedMesh = WorldUvCube(center, size, name);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        go.isStatic = true;
        return go;
    }

    // Each face: outward normal, the direction that reads as "right" and "up" when you look at it.
    static readonly Vector3[][] Faces = {
        new[] { Vector3.right,   Vector3.forward,  Vector3.up },
        new[] { Vector3.left,    Vector3.back,     Vector3.up },
        new[] { Vector3.forward, Vector3.left,     Vector3.up },
        new[] { Vector3.back,    Vector3.right,    Vector3.up },
        new[] { Vector3.up,      Vector3.right,    Vector3.forward },
        new[] { Vector3.down,    Vector3.right,    Vector3.back },
    };

    // Unit cube (the transform scales it to size) with UV = world position along each face's right/up
    static Mesh WorldUvCube(Vector3 center, Vector3 size, string name)
    {
        var verts = new List<Vector3>(); var normals = new List<Vector3>();
        var uvs = new List<Vector2>(); var tris = new List<int>();
        float[] sr = { -1f, 1f, 1f, -1f }, su = { -1f, -1f, 1f, 1f }; // bottom-left, bottom-right, top-right, top-left

        foreach (var f in Faces)
        {
            Vector3 n = f[0], r = f[1], u = f[2];
            int k = verts.Count;
            for (int i = 0; i < 4; i++)
            {
                Vector3 local = (n + r * sr[i] + u * su[i]) * 0.5f;
                Vector3 world = center + Vector3.Scale(local, size);
                verts.Add(local);
                normals.Add(n);
                uvs.Add(new Vector2(Vector3.Dot(world, r), Vector3.Dot(world, u)));
            }
            // Clockwise as seen from outside, which is Unity's front face
            tris.AddRange(new[] { k, k + 3, k + 2, k, k + 2, k + 1 });
        }

        var mesh = new Mesh { name = name };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateTangents(); // needed for normal maps
        mesh.RecalculateBounds();
        Unwrapping.GenerateSecondaryUVSet(mesh); // lightmap UVs, in case the team bakes lighting later
        return mesh;
    }

    static Transform Child(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    // Shows a colored name label in the Scene view (0 grey, 1 blue, 3 green, 4 yellow, 6 red, 7 purple)
    static void SetIcon(GameObject go, int labelColor)
    {
        var icon = EditorGUIUtility.IconContent($"sv_label_{labelColor}").image as Texture2D;
        if (icon != null) EditorGUIUtility.SetIconForObject(go, icon);
    }

    // Rectangle minus a list of holes, returned as non-overlapping rectangles
    static List<Rect2> SubtractAll(Rect2 area, List<Rect2> holes)
    {
        var result = new List<Rect2> { area };
        foreach (var h in holes)
        {
            var next = new List<Rect2>();
            foreach (var r in result) next.AddRange(Subtract(r, h));
            result = next;
        }
        return result;
    }

    static IEnumerable<Rect2> Subtract(Rect2 r, Rect2 h)
    {
        bool overlaps = h.x0 < r.x1 && h.x1 > r.x0 && h.z0 < r.z1 && h.z1 > r.z0;
        if (!overlaps) { yield return r; yield break; }
        float cx0 = Mathf.Max(r.x0, h.x0), cx1 = Mathf.Min(r.x1, h.x1);
        float cz0 = Mathf.Max(r.z0, h.z0), cz1 = Mathf.Min(r.z1, h.z1);
        if (cx0 > r.x0) yield return new Rect2(r.x0, r.z0, cx0, r.z1);   // strip to the west
        if (cx1 < r.x1) yield return new Rect2(cx1, r.z0, r.x1, r.z1);   // strip to the east
        if (cz0 > r.z0) yield return new Rect2(cx0, r.z0, cx1, cz0);     // strip to the south
        if (cz1 < r.z1) yield return new Rect2(cx0, cz1, cx1, r.z1);     // strip to the north
    }

    // House materials, saved as assets so they survive closing and reopening the scene.
    // A material that already exists is reused untouched, so textures assigned by hand are kept.
    class Materials
    {
        readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        public Materials()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Generated", "HouseMaterials");
        }

        public Material Get(string name)
        {
            Material mat;
            if (cache.TryGetValue(name, out mat)) return mat;

            string path = $"{MaterialFolder}/{name}.mat";
            mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader) { name = name };
                var color = MaterialColors[name];
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color); // URP
                else mat.color = color;                                               // built-in fallback
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f); // old, matte surfaces
                AssetDatabase.CreateAsset(mat, path);
            }
            cache[name] = mat;
            return mat;
        }
    }
}
