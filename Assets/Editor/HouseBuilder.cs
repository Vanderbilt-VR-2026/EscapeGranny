// HouseBuilder.cs
//
// Builds the Escape the Granny house (greybox) straight from the floor plan numbers,
// so nobody has to place walls, floors and stairs by hand.
//
// How to use:
//   1. Put this file in Assets/Editor/ (the folder name "Editor" matters).
//   2. In Unity, open the scene you want the house in.
//   3. Menu: Tools > Escape Granny > Build House.
//   Run it again any time; it deletes the old house and builds a fresh one.
//
// All numbers are meters and match the floor plan PDF:
//   origin (0, 0) = south-west outer corner, +x east, +z north, +y up.
//   basement floor y = -3, ground floor y = 0, upper floor y = +3, roof y = +6.
// If the plan changes, edit the numbers in the data section below and rebuild.

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HouseBuilder
{
    // ------------------------------------------------------------------
    // Building constants
    // ------------------------------------------------------------------
    const string RootName = "House (generated)";
    const float WallThickness = 0.2f;
    const float SlabThickness = 0.2f;
    const float FloorToFloor = 3f;
    const float WallHeight = FloorToFloor - SlabThickness; // 2.8 m clear, the slab above sits on top
    const float DoorHeight = 2.2f;
    const float RailHeight = 1f;
    const float RailThickness = 0.06f;

    // Every flight: 16 risers of 0.1875 m. We model 15 steps; the 16th "step" is the floor above.
    const int StepCount = 15;
    const float Tread = 0.28f;
    const float Rise = FloorToFloor / (StepCount + 1);
    const float Run = StepCount * Tread; // 4.2 m

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
        public Rect2 footprint;
        public List<Rect2> floorHoles = new List<Rect2>();
        public List<Wall> walls = new List<Wall>();
        public List<Gap> gaps = new List<Gap>();
        public List<Wall> rails = new List<Wall>();
        public List<Marker> markers = new List<Marker>();
    }

    // ------------------------------------------------------------------
    // Floor plan data (same numbers as the PDF)
    // ------------------------------------------------------------------
    static List<Level> BuildPlan()
    {
        var levels = new List<Level>();

        // ---------------- Upper floor, y = +3 ----------------
        var up = new Level { name = "Upper Floor", y = 3f, footprint = new Rect2(0, 0, 18, 14) };
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
        var gr = new Level { name = "Ground Floor", y = 0f, footprint = new Rect2(0, 0, 18, 14) };
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
        var bs = new Level { name = "Basement", y = -3f, footprint = new Rect2(4, 0, 14, 7) };
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
    const float RoofY = 6f;

    // Escape trigger just outside the front door (x 13.5 to 16.5, z 14.3 to 15.4)
    static readonly Rect2 EscapeZone = new Rect2(13.5f, 14.3f, 16.5f, 15.4f);

    // ------------------------------------------------------------------
    // Menu commands
    // ------------------------------------------------------------------
    [MenuItem("Tools/Escape Granny/Build House")]
    public static void BuildHouse()
    {
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

            // Floor slab with holes cut for the stairs
            var floorParent = Child(structure, "Floor");
            foreach (var r in SubtractAll(level.footprint, level.floorHoles))
            {
                Slab(floorParent, "Floor", r, level.y, mats.floor);
                pieces++;
            }

            // Walls, with door and archway gaps cut out, plus a lintel above each gap
            var wallParent = Child(structure, "Walls");
            foreach (var w in level.walls) pieces += BuildWall(wallParent, w, level.gaps, level.y, mats.wall);
            foreach (var g in level.gaps)
            {
                Lintel(wallParent, g, level.y, mats.wall);
                pieces++;
                if (g.isDoor) { DoorSlot(doors, g, level.y); markers++; }
            }

            // Railings around stair openings
            if (level.rails.Count > 0)
            {
                var railParent = Child(structure, "Railings");
                foreach (var r in level.rails) { Railing(railParent, r, level.y, mats.rail); pieces++; }
            }

            // Spawn, key and hiding spot markers
            foreach (var m in level.markers) { GameplayMarker(gameplay, m, level.y); markers++; }
        }

        // Roof: same as a floor slab, sitting on top of the upper floor walls
        var roof = Child(root.transform, "Roof");
        Slab(roof, "Roof", RoofFootprint, RoofY, mats.floor);
        pieces++;

        // Staircases
        var stairsParent = Child(root.transform, "Stairs");
        foreach (var s in Stairs) { BuildStair(stairsParent, s, mats.stairs); pieces += StepCount + 1; }

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
        Debug.Log($"[HouseBuilder] Built house: {pieces} structure pieces, {markers} markers. " +
                  "Tip: move your XR Origin to the ground hallway, around (7.5, 0, 6.5), to test.");
    }

    [MenuItem("Tools/Escape Granny/Remove House")]
    public static void RemoveHouse()
    {
        var old = GameObject.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old);
    }

    // ------------------------------------------------------------------
    // Geometry helpers
    // ------------------------------------------------------------------

    // Splits a wall into solid pieces around its gaps. A piece only gets the half-thickness
    // extension at a real wall end (so corners close), never at the edge of a door gap.
    static int BuildWall(Transform parent, Wall w, List<Gap> gaps, float levelY, Material mat)
    {
        var cuts = new List<Gap>();
        foreach (var g in gaps)
            if (g.o == w.o && Mathf.Abs(g.line - w.line) < 0.001f && g.to > w.from && g.from < w.to) cuts.Add(g);
        cuts.Sort((p, q) => p.from.CompareTo(q.from));

        int count = 0;
        float start = w.from;
        bool startIsWallEnd = true;
        foreach (var g in cuts)
        {
            if (g.from > start) { WallPiece(parent, w, start, g.from, startIsWallEnd, false, levelY, mat); count++; }
            start = g.to;
            startIsWallEnd = false;
        }
        if (start < w.to) { WallPiece(parent, w, start, w.to, startIsWallEnd, true, levelY, mat); count++; }
        return count;
    }

    static void WallPiece(Transform parent, Wall w, float a, float b, bool extendA, bool extendB, float levelY, Material mat)
    {
        float half = WallThickness / 2f;
        float lo = a - (extendA ? half : 0f);
        float hi = b + (extendB ? half : 0f);
        float mid = (lo + hi) / 2f, len = hi - lo;
        var center = w.o == 'h'
            ? new Vector3(mid, levelY + WallHeight / 2f, w.line)
            : new Vector3(w.line, levelY + WallHeight / 2f, mid);
        var size = w.o == 'h'
            ? new Vector3(len, WallHeight, WallThickness)
            : new Vector3(WallThickness, WallHeight, len);
        Box(parent, $"Wall {w.o}{w.line} [{a}-{b}]", center, size, mat);
    }

    // Wall piece above a door or archway, from door height up to the ceiling
    static void Lintel(Transform parent, Gap g, float levelY, Material mat)
    {
        float h = WallHeight - DoorHeight;
        float mid = (g.from + g.to) / 2f, len = g.to - g.from;
        var center = g.o == 'h'
            ? new Vector3(mid, levelY + DoorHeight + h / 2f, g.line)
            : new Vector3(g.line, levelY + DoorHeight + h / 2f, mid);
        var size = g.o == 'h' ? new Vector3(len, h, WallThickness) : new Vector3(WallThickness, h, len);
        Box(parent, $"Lintel {g.id}", center, size, mat);
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

    // Slab whose top surface sits exactly at topY
    static void Slab(Transform parent, string name, Rect2 r, float topY, Material mat)
    {
        var center = new Vector3((r.x0 + r.x1) / 2f, topY - SlabThickness / 2f, (r.z0 + r.z1) / 2f);
        var size = new Vector3(r.x1 - r.x0, SlabThickness, r.z1 - r.z0);
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
    static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); // comes with a BoxCollider
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        go.isStatic = true;
        return go;
    }

    static Transform Child(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static Transform Child(GameObject parent, string name) => Child(parent.transform, name);

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

    // Greybox materials, saved as assets so they survive closing and reopening the scene
    class Materials
    {
        public Material floor, wall, stairs, rail;
        const string Folder = "Assets/Generated/HouseMaterials";

        public Materials()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Generated", "HouseMaterials");
            floor = Get("Greybox Floor", new Color(0.55f, 0.55f, 0.55f));
            wall = Get("Greybox Wall", new Color(0.82f, 0.82f, 0.82f));
            stairs = Get("Greybox Stairs", new Color(0.45f, 0.42f, 0.40f));
            rail = Get("Greybox Rail", new Color(0.25f, 0.25f, 0.25f));
        }

        static Material Get(string name, Color color)
        {
            string path = $"{Folder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color); // URP
            else mat.color = color;                                               // built-in fallback
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
