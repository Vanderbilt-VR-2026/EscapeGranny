// MenuRoomPicker.cs
//
// Starts the main menu in Granny's house, with the menu floating in front of the player.
// By default it's always the same open spot (the big living room, facing the front door), because in
// small random rooms the menu could end up half through a wall. Untick Use Fixed Spot to go back to a
// random room each time.
//
// The fixed spot is where the player's HEAD ends up, not the rig: the headset can be a metre or two
// from the centre of the play area and facing any way, so moving only the rig could still leave the
// player inside a wall. Once the headset is tracking, the rig is shifted and turned so the head is on
// the spot, facing Fixed Yaw. This runs before HeadsetMenuPlacer (execution order), so the menu then
// appears straight ahead in open space.
//
// It picks a random spawn marker (by default the prisoner and Granny spawns, which sit in the middle
// of real rooms; hiding spots would put you inside a wardrobe), then turns the player toward the
// most open direction of that room, so the menu has space in front of them.
//
// Setup: put this on its own GameObject in the menu scene, drag in the XR Origin. The scene needs the
// house with its "Gameplay Markers" (MainMenuSceneBuilder builds both for you).

using UnityEngine;

[DefaultExecutionOrder(-100)]
public class MenuRoomPicker : MonoBehaviour
{
    [SerializeField] private Transform rig;
    [Tooltip("Always start at the spot below instead of a random room.")]
    [SerializeField] private bool useFixedSpot = true;
    [Tooltip("Where the player stands (house coordinates, m). Default: living room, clear of the stairs.")]
    [SerializeField] private Vector3 fixedPosition = new Vector3(14.5f, 0f, 4f);
    [Tooltip("Which way the player faces (degrees, 0 = north toward the front door, ~10 m of open floor).")]
    [SerializeField] private float fixedYaw = 0f;
    [Tooltip("Snap anyway after this many seconds if the headset never reports tracking (e.g. Editor).")]
    [SerializeField, Min(0f)] private float maxWaitSeconds = 2.5f;
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

        if (useFixedSpot)
        {
            // Rough move now so the first frames aren't somewhere random; LateUpdate fixes it to the head.
            OfflineGameLauncher.MoveRig(rig.gameObject, fixedPosition, Quaternion.Euler(0f, fixedYaw, 0f));
            var cam = rig.GetComponentInChildren<Camera>();
            head = cam != null ? cam.transform : null;
            return;
        }

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

    private Transform head;
    private int trackedFrames;
    private float waited;
    private bool snapped;

    // Fewer stable frames than HeadsetMenuPlacer needs (3), so the head is on the spot before the menu is placed.
    private const int StableFramesNeeded = 2;

    private void LateUpdate()
    {
        if (!useFixedSpot || snapped || rig == null || head == null) return;

        trackedFrames = HeadsetMenuPlacer.IsHeadsetTracked() ? trackedFrames + 1 : 0;
        waited += Time.unscaledDeltaTime;
        if (trackedFrames < StableFramesNeeded && waited < maxWaitSeconds) return;

        // Turn the rig around the head so the player faces Fixed Yaw, then slide it so the head is
        // over the spot. Height stays: the rig keeps standing on the floor.
        var body = rig.GetComponent<CharacterController>();
        if (body != null) body.enabled = false;
        rig.RotateAround(head.position, Vector3.up, Mathf.DeltaAngle(head.eulerAngles.y, fixedYaw));
        rig.position += new Vector3(fixedPosition.x - head.position.x, 0f, fixedPosition.z - head.position.z);
        if (body != null) OfflineGameLauncher.ReEnableBody(body);

        snapped = true;
        Debug.Log($"[MenuRoomPicker] Player's head placed at {fixedPosition}, facing {fixedYaw:0} degrees.");
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
