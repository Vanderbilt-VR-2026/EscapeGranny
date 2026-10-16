// MenuRoomPicker.cs
//
// Starts the main menu in a random room of Granny's house: the player opens the game standing in a
// dark room, a different one each time, with the menu floating in front of them.
//
// It picks a random spawn marker (by default the prisoner and Granny spawns, which sit in the middle
// of real rooms; hiding spots would put you inside a wardrobe), then turns the player toward the
// most open direction of that room, so the menu has space in front of them.
//
// Setup: put this on its own GameObject in the menu scene, drag in the XR Origin. The scene needs the
// house with its "Gameplay Markers" (MainMenuSceneBuilder builds both for you).

using UnityEngine;

public class MenuRoomPicker : MonoBehaviour
{
    [SerializeField] private Transform rig;
    [Tooltip("Marker groups to pick a room from (names HouseBuilder uses).")]
    [SerializeField] private string[] markerGroups = { "Prisoner Spawns", "Granny Spawns" };
    [Tooltip("Height of the room-scanning rays above the floor (m), roughly eye height.")]
    [SerializeField, Min(0.2f)] private float scanHeight = 1.5f;
    [Tooltip("Directions checked when looking for the most open way to face.")]
    [SerializeField, Range(4, 64)] private int scanDirections = 16;

    // Start, not Awake: every collider in the scene is ready by then, and it still runs before
    // HeadsetMenuPlacer places the menu (that waits for headset tracking).
    private void Start()
    {
        if (rig == null) return;

        var spots = new System.Collections.Generic.List<Transform>();
        foreach (string group in markerGroups) spots.AddRange(OfflineGameLauncher.FindSpawns(group));
        if (spots.Count == 0)
        {
            Debug.LogWarning("[MenuRoomPicker] No spawn markers found; the player stays where the scene put them.", this);
            return;
        }

        var spot = spots[Random.Range(0, spots.Count)];
        float yaw = MostOpenYaw(spot.position + Vector3.up * scanHeight);
        OfflineGameLauncher.MoveRig(rig.gameObject, spot.position, Quaternion.Euler(0f, yaw, 0f));
        Debug.Log($"[MenuRoomPicker] Menu starts at {spot.name}, facing {yaw:0} degrees.");
    }

    // The direction (degrees around the vertical axis) with the farthest wall.
    private float MostOpenYaw(Vector3 from)
    {
        const float maxRange = 20f;
        float bestYaw = 0f, bestDistance = -1f;
        for (int i = 0; i < scanDirections; i++)
        {
            float yaw = i * 360f / scanDirections;
            var direction = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            float distance = Physics.Raycast(from, direction, out var hit, maxRange, ~0, QueryTriggerInteraction.Ignore)
                ? hit.distance : maxRange;
            if (distance > bestDistance)
            {
                bestDistance = distance;
                bestYaw = yaw;
            }
        }
        return bestYaw;
    }
}
