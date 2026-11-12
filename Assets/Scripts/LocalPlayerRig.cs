
using System.Collections;
using Fusion;
using UnityEngine;

// Attach this to the Network Player prefab root.
public class LocalPlayerRig : NetworkBehaviour
{
    private const float k_ExitThreshold = 0.7f;

    private SceneRigReferences rig;
    private PlayerHideState hideState;
    private HidingSpot currentSpot;
    private bool stickWasForward;

    public override void Spawned()
    {
        if (!HasInputAuthority)
            return;

        hideState = GetComponent<PlayerHideState>();

        rig = FindFirstObjectByType<SceneRigReferences>();

        if (rig == null)
        {
            Debug.LogError(
                "No SceneRigReferences found. Add it to the XR Origin."
            );
            return;
        }

        // Register this local player's VR rig.
        HidingSpot.LocalRig = this;

        if (rig.exitAction != null && rig.exitAction.action != null)
        {
            rig.exitAction.action.Enable();
        }
        else
        {
            Debug.LogError("Exit Action is not assigned in SceneRigReferences.");
        }

        if (hideState == null)
        {
            Debug.LogError("PlayerHideState is missing from Network Player.");
        }
    }

    private void Update()
    {
        if (!HasInputAuthority || rig == null || currentSpot == null)
            return;

        if (rig.exitAction == null || rig.exitAction.action == null)
            return;

        // Read the left joystick.
        Vector2 stick = rig.exitAction.action.ReadValue<Vector2>();

        bool stickForward = stick.y > k_ExitThreshold;

        // Exit only when the joystick first crosses the threshold.
        if (stickForward && !stickWasForward)
        {
            ExitHiding(currentSpot.ExitPoint);
        }

        stickWasForward = stickForward;
    }

    // Called when the player selects the Hide prompt.
    public void EnterHiding(Transform hidePoint, HidingSpot spot)
    {
        if (rig == null || hidePoint == null || spot == null)
            return;

        currentSpot = spot;

        // Prevent the joystick from immediately triggering an exit.
        stickWasForward =
            rig.exitAction.action.ReadValue<Vector2>().y > k_ExitThreshold;

        // Disable normal player locomotion.
        SetLocomotion(false);

        // Move the VR player to the hiding position.
        MoveTo(hidePoint);

        // Synchronize invisibility through Photon Fusion.
        if (hideState != null)
        {
            hideState.RequestHide();
        }

        StartCoroutine(LogAfterMove("hide"));

        Debug.Log("[Hide] Player entered hiding spot.");
    }

    // Called when the player pushes the joystick forward.
    public void ExitHiding(Transform exitPoint)
    {
        if (rig == null || currentSpot == null)
            return;

        HidingSpot previousSpot = currentSpot;

        // Move the player out of the hiding position.
        if (exitPoint != null)
        {
            MoveTo(exitPoint);
        }

        // Restore normal locomotion.
        SetLocomotion(true);

        // Make the player visible to other players.
        if (hideState != null)
        {
            hideState.RequestUnhide();
        }

        // Make the bed available again locally.
        previousSpot.Release();

        currentSpot = null;

        StartCoroutine(LogAfterMove("exit"));

        Debug.Log("[Hide] Player exited hiding spot.");
    }

    // Move the player's feet to the hiding or exit position.
    private void MoveTo(Transform target)
    {
        var origin = rig.xrOrigin;

        rig.characterController.enabled = false;

        // Face the direction specified by the target.
        Vector3 flat = Vector3.ProjectOnPlane(
            target.forward,
            Vector3.up
        );

        if (flat.sqrMagnitude < 0.001f)
            flat = Vector3.forward;

        origin.MatchOriginUpCameraForward(
            Vector3.up,
            flat.normalized
        );

        // Find the floor underneath the target.
        Vector3 floorPos = target.position;

        if (Physics.Raycast(
            target.position + Vector3.up * 0.5f,
            Vector3.down,
            out var hit,
            3f,
            ~0,
            QueryTriggerInteraction.Ignore))
        {
            floorPos = hit.point;

            Debug.Log(
                $"[Hide] Floor hit: {hit.collider.name} at Y={hit.point.y:F2}"
            );
        }
        else
        {
            Debug.LogWarning(
                "[Hide] No floor found under the point. Check the floor collider."
            );
        }

        floorPos += Vector3.up * 0.05f;

        // Preserve the headset's horizontal offset from the XR Origin.
        Vector3 cam = origin.CameraInOriginSpacePos;

        Vector3 offset = origin.Origin.transform.TransformVector(
            new Vector3(cam.x, 0f, cam.z)
        );

        origin.Origin.transform.position = floorPos - offset;

        rig.characterController.enabled = true;
    }

    // Enable or disable regular VR locomotion.
    private void SetLocomotion(bool on)
    {
        foreach (var b in rig.locomotionToDisable)
        {
            if (b != null)
                b.enabled = on;
        }
    }

    private IEnumerator LogAfterMove(string label)
    {
        var t = rig.xrOrigin.Origin.transform;

        Debug.Log(
            $"[Hide] {label}: origin Y right after move = {t.position.y:F2}"
        );

        yield return new WaitForSeconds(1f);

        Debug.Log(
            $"[Hide] {label}: origin Y after 1s = {t.position.y:F2}"
        );
    }

    private void OnDestroy()
    {
        if (HidingSpot.LocalRig == this)
        {
            HidingSpot.LocalRig = null;
        }
    }
}

