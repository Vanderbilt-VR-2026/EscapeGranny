// HeadsetMenuPlacer.cs
//
// Shows a menu straight ahead of the player, once the headset is really tracking.
//
// Why: for the first few frames of a VR app the camera still sits at its default pose. Unity only
// receives the headset's real position and rotation a little later, so a menu placed on frame one can
// end up beside or behind the player. This waits for valid headset tracking, places the menu once
// along the player's horizontal facing (same as the class tutorial's Lobby panel), then leaves it
// there so the player can look around freely.
//
// Walls: if the player is facing a wall when the menu appears and Rig is set, the rig is turned once
// (around the player's head) so the most open side of the room is straight ahead. If the open space
// is still shorter than Distance, the menu comes in front of the wall and shrinks by the same amount,
// so it looks the same size and never ends up inside or behind the wall.
//
// Setup: put this on its own GameObject in the scene (not on the menu, because the menu is hidden
// until it is placed). Drag the menu canvas into Menu, the rig's Main Camera into Head, and
// (optional) the XR Origin into Rig.
// MainMenuSceneBuilder does this for you.

using UnityEngine;
using UnityEngine.XR;

public class HeadsetMenuPlacer : MonoBehaviour
{
    [SerializeField] private Transform menu;
    [Tooltip("The XR rig's Main Camera. Left empty, Camera.main is used.")]
    [SerializeField] private Transform head;
    [Tooltip("Optional: the XR Origin, turned once to face open space if the player starts facing a wall.")]
    [SerializeField] private Transform rig;

    [Tooltip("How far in front of the player the menu appears (m).")]
    [SerializeField, Min(0.3f)] private float distance = 2f;
    [Tooltip("Menu centre relative to eye height (m). Negative = a little below eye level, which is comfier to read.")]
    [SerializeField] private float heightOffset = -0.1f;
    [Tooltip("Tracked frames in a row before the pose is trusted (the very first one can still be stale).")]
    [SerializeField, Min(1)] private int stableFramesNeeded = 3;
    [Tooltip("Place the menu anyway after this many seconds, so it never stays hidden (e.g. Editor with no headset).")]
    [SerializeField, Min(0f)] private float maxWaitSeconds = 3f;
    [Tooltip("Gap kept between the menu and a wall behind it (m).")]
    [SerializeField, Min(0f)] private float wallMargin = 0.3f;
    [Tooltip("Never closer than this (m). Only matters in a space smaller than this plus Wall Margin.")]
    [SerializeField, Min(0.2f)] private float minDistance = 0.5f;
    [Tooltip("How wide a gap must be to count as open (m): a thin ray would squeeze through a door crack.")]
    [SerializeField, Min(0f)] private float clearanceRadius = 0.25f;
    [Tooltip("Eye height used when the headset reports one lower than this above the floor (m), e.g. before "
           + "its height arrives or in the XR Device Simulator. Seated players are still about 1.1 m.")]
    [SerializeField, Min(0.5f)] private float minEyeHeight = 1.1f;
    [Tooltip("Half the menu's height at full size (m): its bottom edge is kept above the floor.")]
    [SerializeField, Min(0f)] private float menuHalfHeight = 0.6f;

    private const InputTrackingState PoseTracked = InputTrackingState.Position | InputTrackingState.Rotation;

    private int trackedFrames;
    private float waited;
    private bool placed;
    private Vector3 baseScale;
    private Vector3 eye; // the player's eye point used for placing, see Place()

    private void Awake()
    {
        if (head == null && Camera.main != null) head = Camera.main.transform;
        if (menu != null) baseScale = menu.localScale;
        // Hidden until placed, so the player never sees it jump.
        if (menu != null) menu.gameObject.SetActive(false);
    }

    // LateUpdate runs after the rig's Tracked Pose Driver has moved the camera this frame.
    private void LateUpdate()
    {
        if (placed || menu == null || head == null) return;

        trackedFrames = IsHeadsetTracked() ? trackedFrames + 1 : 0;
        waited += Time.unscaledDeltaTime;
        if (trackedFrames >= stableFramesNeeded || waited >= maxWaitSeconds)
            Place();
    }

    private void Place()
    {
        // Eye point measured from the floor: the headset's height can arrive later than its rotation,
        // and a menu centred on a too-low head ends up half in the ground.
        float floorY = FloorHeight();
        eye = head.position;
        eye.y = floorY + Mathf.Max(head.position.y - floorY, minEyeHeight);

        // Only the left/right heading: looking up or down must not tilt the menu or move it vertically.
        float yaw = head.eulerAngles.y;
        float free = FreeDistance(yaw);

        // Facing a wall? Turn the player (the rig, around their head) toward the most open side, once.
        if (rig != null && free < distance + wallMargin)
        {
            float bestYaw = yaw;
            for (int step = 1; step < 24; step++)
            {
                float candidate = yaw + step * 15f;
                float candidateFree = FreeDistance(candidate);
                if (candidateFree > free)
                {
                    free = candidateFree;
                    bestYaw = candidate;
                }
            }
            TurnRig(bestYaw - yaw);
            yaw = bestYaw;
        }

        var heading = Quaternion.Euler(0f, yaw, 0f);
        var forward = heading * Vector3.forward;
        // Never past the wall: closer (and smaller) if the open space is short.
        float placedDistance = Mathf.Clamp(free - wallMargin, minDistance, distance);

        float scale = placedDistance / distance;
        var position = eye + forward * placedDistance;
        position.y = eye.y + heightOffset * scale;
        // Bottom edge above the floor, whatever the eye height.
        position.y = Mathf.Max(position.y, floorY + menuHalfHeight * scale + 0.05f);

        menu.SetPositionAndRotation(position, heading);
        menu.localScale = baseScale * scale; // closer but smaller = same size to the eye
        menu.gameObject.SetActive(true);
        placed = true;
        enabled = false; // Fixed from now on.
    }

    // How far the player can see along this heading before something solid, up to the menu distance.
    private float FreeDistance(float yaw)
    {
        var direction = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        float range = distance + wallMargin;
        return Physics.SphereCast(eye, clearanceRadius, direction, out var hit, range, ~0, QueryTriggerInteraction.Ignore)
            ? hit.distance : range;
    }

    // Height of the floor under the player: the rig stands on it; without a rig, look straight down.
    private float FloorHeight()
    {
        if (rig != null) return rig.position.y;
        return Physics.Raycast(head.position + Vector3.up * 0.1f, Vector3.down, out var hit, 5f, ~0, QueryTriggerInteraction.Ignore)
            ? hit.point.y : head.position.y - 1.5f;
    }

    // Turns the rig around the player's head, so the player stays where they are and only their
    // facing in the room changes.
    private void TurnRig(float degrees)
    {
        if (Mathf.Approximately(degrees, 0f)) return;
        // A CharacterController ignores direct transform changes while enabled, so switch it off for the turn.
        var body = rig.GetComponent<CharacterController>();
        if (body != null) body.enabled = false;
        rig.RotateAround(head.position, Vector3.up, degrees);
        if (body != null) OfflineGameLauncher.ReEnableBody(body);
    }

    // True once the headset reports both its position and rotation. Also used by the Lobby panel
    // (FusionNetworkManager), so both menus wait for the same thing.
    public static bool IsHeadsetTracked()
    {
        // A real headset (Quest over OpenXR) shows up in Unity's XR input API.
        var device = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if (device.isValid)
        {
            if (device.TryGetFeatureValue(CommonUsages.trackingState, out InputTrackingState state))
                return (state & PoseTracked) == PoseTracked;
            if (device.TryGetFeatureValue(CommonUsages.isTracked, out bool isTracked))
                return isTracked;
            return true; // Headset doesn't report tracking at all; trust that it is there.
        }

        // The XR Device Simulator's fake headset (for Macs) only exists in the Input System.
        var simulated = UnityEngine.InputSystem.InputSystem.GetDevice<UnityEngine.InputSystem.XR.XRHMD>();
        return simulated != null && simulated.isTracked.isPressed;
    }
}
