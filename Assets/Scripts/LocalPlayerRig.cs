using System.Collections;
using Fusion;
using UnityEngine;

// PUT THIS ON THE NETWORK PLAYER PREFAB ROOT. No fields to fill in.
public class LocalPlayerRig : NetworkBehaviour
{
    const float k_ExitThreshold = 0.7f; // how far forward the left stick must be pushed to exit

    SceneRigReferences rig;
    PlayerHideState hideState;
    HidingSpot currentSpot;
    bool stickWasForward;

    public override void Spawned()
    {
        if (!HasInputAuthority) return; // in Shared Mode, try HasStateAuthority if LocalRig stays null

        hideState = GetComponent<PlayerHideState>();

        rig = FindFirstObjectByType<SceneRigReferences>(); // FindObjectOfType on older Unity
        if (rig == null)
        {
            Debug.LogError("No SceneRigReferences found in the scene. Add it to the XR Origin.");
            return;
        }

        HidingSpot.LocalRig = this;
        rig.exitAction.action.Enable();
    }

    void Update()
    {
        if (!HasInputAuthority || rig == null || currentSpot == null) return;

        // Exit by pushing the left stick forward (exitAction = XRI Left Locomotion/Move, a Vector2)
        Vector2 stick = rig.exitAction.action.ReadValue<Vector2>();
        bool stickForward = stick.y > k_ExitThreshold;

        if (stickForward && !stickWasForward)
            currentSpot.RPC_RequestExit();

        stickWasForward = stickForward;
    }

    public void EnterHiding(Transform hidePoint, HidingSpot spot)
    {
        if (rig == null) return;

        currentSpot = spot;
        // If the stick is already pushed forward on entering, don't exit immediately
        stickWasForward = rig.exitAction.action.ReadValue<Vector2>().y > k_ExitThreshold;

        if (hideState != null) hideState.SetHidden(true);
        SetLocomotion(false);
        MoveTo(hidePoint, true);
        StartCoroutine(LogAfterMove("hide"));
    }

    public void ExitHiding(Transform exitPoint)
    {
        if (rig == null) return;

        MoveTo(exitPoint, false);
        SetLocomotion(true);
        if (hideState != null) hideState.SetHidden(false);
        currentSpot = null;
        StartCoroutine(LogAfterMove("exit"));
    }

    // Puts the player's FEET (the rig origin) on the floor under the point,
    // so the head is never placed at floor level.
    void MoveTo(Transform target, bool headAtTarget)
    {
        var origin = rig.xrOrigin;
        rig.characterController.enabled = false;

        // Face the point's forward direction (flattened so a tilted point can't break it)
        Vector3 flat = Vector3.ProjectOnPlane(target.forward, Vector3.up);
        if (flat.sqrMagnitude < 0.001f) flat = Vector3.forward;
        origin.MatchOriginUpCameraForward(Vector3.up, flat.normalized);

        // Snap to the floor under the point, so a slightly wrong Y can't drop you through
        if (headAtTarget)
        {
            // HIDING: put the player's HEAD at the point (HidePoint = where the head should be).
            // The Character Controller stays disabled while hidden, so nothing can pull you through the floor.
            origin.MoveCameraToWorldLocation(target.position);
            return;
        }

        // EXITING: put the FEET on the floor under the point
        Vector3 floorPos = target.position;
        if (Physics.Raycast(target.position + Vector3.up * 0.5f, Vector3.down,
                            out var hit, 3f, ~0, QueryTriggerInteraction.Ignore))
        {
            floorPos = hit.point;
            Debug.Log($"[Hide] Floor hit: {hit.collider.name} at Y={hit.point.y:F2}");
        }
        else
        {
            Debug.LogWarning("[Hide] No floor found under the point. Check the floor has a collider.");
        }
        floorPos += Vector3.up * 0.05f; // small lift so the capsule starts above the floor

        Vector3 cam = origin.CameraInOriginSpacePos;
        Vector3 offset = origin.Origin.transform.TransformVector(new Vector3(cam.x, 0f, cam.z));
        origin.Origin.transform.position = floorPos - offset;

        rig.characterController.enabled = true;
    }

    void SetLocomotion(bool on)
    {
        foreach (var b in rig.locomotionToDisable) b.enabled = on;
    }

    IEnumerator LogAfterMove(string label)
    {
        var t = rig.xrOrigin.Origin.transform;
        Debug.Log($"[Hide] {label}: origin Y right after move = {t.position.y:F2}");
        yield return new WaitForSeconds(1f);
        Debug.Log($"[Hide] {label}: origin Y after 1s = {t.position.y:F2}");
    }
}