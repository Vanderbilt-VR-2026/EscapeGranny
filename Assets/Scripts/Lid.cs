using UnityEngine;

// Put this on the LID PIVOT (an empty object at the hinge), not on the lid mesh.
// Uses the IInteractable interface defined in Door.cs, so keep Door.cs in the project.
// For multiplayer, also add NetworkToggle (and NetworkObject) to the same pivot.
// Sounds (creak on open, thud on close) are added automatically: see OpenCloseSound.
// Locked: tick Locked to keep the lid shut until something calls UnlockAndOpen(), e.g. a Keypad's
// On Access Granted event (the keypad chest that holds the front-door key).
public class Lid : MonoBehaviour, IInteractable
{
    [SerializeField] private Vector3 openAxis = Vector3.right; // X axis suits a chest hinged at the back
    [SerializeField] private float openAngle = -100f;          // flip the sign if it opens the wrong way
    [SerializeField] private float speed = 3f;
    [SerializeField] private bool isOpen = false;
    [Tooltip("While ticked, grabbing the lid does nothing. UnlockAndOpen() unticks it.")]
    [SerializeField] private bool locked = false;

    private Quaternion closedRotation;
    private NetworkToggle networkToggle;
    private OpenCloseSound sound;
    private bool wasNetworked;

    private void Awake()
    {
        closedRotation = transform.localRotation;
        if (locked) isOpen = false; // a locked lid always starts shut, even if Is Open was ticked

        networkToggle = GetComponent<NetworkToggle>();
        if (networkToggle) networkToggle.SetStartOpen(isOpen);

        // Lids are small, so they use the cabinet creak and thud.
        sound = OpenCloseSound.Ensure(gameObject, OpenCloseSound.Size.Cabinet);
    }

    public void Interact()
    {
        if (locked) return;
        // Online: ask the network to flip it, so both players see the same lid.
        if (networkToggle && networkToggle.IsReady) networkToggle.RequestToggle();
        else isOpen = !isOpen;
    }

    // For a Keypad's On Access Granted event: unlocks the lid and swings it open.
    public void UnlockAndOpen()
    {
        locked = false;
        if (!isOpen) Interact();
    }

    private void Update()
    {
        // Online, follow the shared value; each headset still animates the lid itself.
        bool networked = networkToggle && networkToggle.IsReady;
        if (networked)
        {
            isOpen = networkToggle.IsOpen;
            // The first shared value after joining isn't someone opening the lid, so no sound.
            if (!wasNetworked) sound.SyncSilently(isOpen);
        }
        wasNetworked = networked;

        // Read the angle every frame so Inspector changes apply immediately.
        Quaternion target = isOpen
            ? closedRotation * Quaternion.AngleAxis(openAngle, openAxis)
            : closedRotation;

        transform.localRotation = Quaternion.Slerp(
            transform.localRotation, target, Time.deltaTime * speed);

        // Each headset plays the sound when it sees the state change, so both players hear it.
        sound.Tick(isOpen, Quaternion.Angle(transform.localRotation, closedRotation));
    }
}
