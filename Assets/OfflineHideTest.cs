using System.Collections;
using UnityEngine;

// TEMPORARY: PUT THIS ON THE BED. Tests hiding without networking.
// Hook Hide() to the bed's XR Simple Interactable > Select Entered event.
// Remove it once you test through the lobby with the real LocalPlayerRig.
public class OfflineHideTest : MonoBehaviour
{
    [SerializeField] Transform hidePoint;
    [SerializeField] Transform exitPoint;

    const float k_ExitThreshold = 0.7f;
    SceneRigReferences rig;
    bool hidden;
    bool stickWasForward;

    public void Hide()
    {
        if (hidden) return;
        if (rig == null) rig = FindFirstObjectByType<SceneRigReferences>(); // FindObjectOfType on older Unity
        if (rig == null) { Debug.LogError("[OfflineHide] No SceneRigReferences in the scene."); return; }

        hidden = true;
        rig.exitAction.action.Enable();
        stickWasForward = rig.exitAction.action.ReadValue<Vector2>().y > k_ExitThreshold;
        foreach (var b in rig.locomotionToDisable) b.enabled = false;
        MoveTo(hidePoint, true);
        StartCoroutine(LogAfterMove("hide"));
    }

    void Update()
    {
        if (!hidden || rig == null) return;

        bool stickForward = rig.exitAction.action.ReadValue<Vector2>().y > k_ExitThreshold;
        if (stickForward && !stickWasForward) Exit();
        stickWasForward = stickForward;
    }

    void Exit()
    {
        MoveTo(exitPoint, false);
        foreach (var b in rig.locomotionToDisable) b.enabled = true;
        hidden = false;
        StartCoroutine(LogAfterMove("exit"));
    }

    // Puts the player's FEET (the rig origin) on the floor under the point
    void MoveTo(Transform target, bool headAtTarget)
    {
        var origin = rig.xrOrigin;
        rig.characterController.enabled = false;

        Vector3 flat = Vector3.ProjectOnPlane(target.forward, Vector3.up);
        if (flat.sqrMagnitude < 0.001f) flat = Vector3.forward;
        origin.MatchOriginUpCameraForward(Vector3.up, flat.normalized);

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
            Debug.Log($"[OfflineHide] Floor hit: {hit.collider.name} at Y={hit.point.y:F2}");
        }
        else
        {
            Debug.LogWarning("[OfflineHide] No floor found under the point. Check the floor has a collider.");
        }
        floorPos += Vector3.up * 0.05f; // small lift so the capsule starts above the floor

        Vector3 cam = origin.CameraInOriginSpacePos;
        Vector3 offset = origin.Origin.transform.TransformVector(new Vector3(cam.x, 0f, cam.z));
        origin.Origin.transform.position = floorPos - offset;

        rig.characterController.enabled = true;
    }

    IEnumerator LogAfterMove(string label)
    {
        var t = rig.xrOrigin.Origin.transform;
        Debug.Log($"[OfflineHide] {label}: origin Y right after move = {t.position.y:F2}");
        yield return new WaitForSeconds(1f);
        Debug.Log($"[OfflineHide] {label}: origin Y after 1s = {t.position.y:F2}");
    }
}