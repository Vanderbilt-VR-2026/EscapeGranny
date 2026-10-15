using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Unity.XR.CoreUtils;

/// <summary>
/// Put on the XR Origin (XR Rig) root. Assign a SpawnPoint (an empty object in the middle of the room).
/// No matter where you stand in real life, your HEAD is moved to the SpawnPoint when the app starts
/// and again whenever you recenter (hold the Meta button).
/// </summary>
[RequireComponent(typeof(XROrigin))]
public class SpawnInRoom : MonoBehaviour
{
    [Tooltip("Empty object in the middle of the room. Its blue (forward) arrow is the direction you will face.")]
    [SerializeField] Transform spawnPoint;
    [Tooltip("Seconds to wait at start so headset tracking is ready.")]
    [SerializeField] float startDelay = 0.75f;
    [SerializeField] bool recenterWithMetaButton = true;

    XROrigin xrOrigin;
    readonly List<XRInputSubsystem> subsystems = new List<XRInputSubsystem>();

    void Awake() => xrOrigin = GetComponent<XROrigin>();

    IEnumerator Start()
    {
        yield return new WaitForSeconds(startDelay);
        Place();
        // Tracking can settle a moment later, so place once more.
        yield return new WaitForSeconds(0.75f);
        Place();
    }

    void OnEnable()
    {
        SubsystemManager.GetSubsystems(subsystems);
        foreach (var s in subsystems) s.trackingOriginUpdated += OnRecentered;
    }

    void OnDisable()
    {
        foreach (var s in subsystems) s.trackingOriginUpdated -= OnRecentered;
        subsystems.Clear();
    }

    void OnRecentered(XRInputSubsystem s)
    {
        if (recenterWithMetaButton) Place();
    }

    [ContextMenu("Place Now")]
    public void Place()
    {
        if (spawnPoint == null || xrOrigin == null || xrOrigin.Camera == null) return;

        // Turn so the head faces the spawn point's forward, then slide so the head is over the spawn point.
        Vector3 forward = Vector3.ProjectOnPlane(spawnPoint.forward, Vector3.up).normalized;
        xrOrigin.MatchOriginUpCameraForward(Vector3.up, forward);

        Vector3 head = xrOrigin.Camera.transform.position;
        xrOrigin.MoveCameraToWorldLocation(new Vector3(spawnPoint.position.x, head.y, spawnPoint.position.z));
    }
}
