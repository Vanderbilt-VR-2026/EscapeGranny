using UnityEngine;

public class OfflineHideTest : MonoBehaviour
{
    [SerializeField] HidingSpot spot;
    [SerializeField] Transform hidePoint;

    public void Hide()
    {
        var rig = FindFirstObjectByType<SceneRigReferences>();
        rig.characterController.enabled = false;
        rig.xrOrigin.MoveCameraToWorldLocation(hidePoint.position);
        rig.xrOrigin.MatchOriginUpCameraForward(Vector3.up, hidePoint.forward);
        rig.characterController.enabled = true;
    }
}