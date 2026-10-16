using UnityEngine;
using Unity.XR.CoreUtils;

/// <summary>
/// Put on an empty object OUTSIDE the door with a Box Collider set to Is Trigger.
/// When the player walks into it (after the door is open) the congratulations panel appears in front of them.
/// </summary>
[RequireComponent(typeof(Collider))]
public class EscapeExit : MonoBehaviour
{
    [SerializeField] EscapeDoor door;
    [Tooltip("World Space canvas with the 'Congratulations' text. Leave it disabled in the scene.")]
    [SerializeField] GameObject congratsPanel;
    [SerializeField] float distanceInFront = 1.8f;
    [SerializeField] float heightOffset = 0f;
    [SerializeField] AudioSource successSound;

    bool shown;

    void Reset() => GetComponent<Collider>().isTrigger = true;

    void Start()
    {
        if (congratsPanel != null) congratsPanel.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (shown) return;
        if (door != null && !door.IsOpen) return;
        if (other.GetComponentInParent<XROrigin>() == null && !other.CompareTag("Player")) return;

        Show();
    }

    [ContextMenu("Show")]
    public void Show()
    {
        shown = true;
        if (successSound != null) successSound.Play();
        if (congratsPanel == null) return;

        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 flat = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            Vector3 pos = cam.transform.position + flat * distanceInFront;
            pos.y = cam.transform.position.y + heightOffset;
            congratsPanel.transform.position = pos;
            congratsPanel.transform.rotation = Quaternion.LookRotation(flat, Vector3.up);   // text faces the player
        }
        congratsPanel.SetActive(true);
    }
}
