using UnityEngine;

public interface IInteractable
{
    void Interact();
}

// Put this on the door's PIVOT object (the hinge), not the mesh itself.
// For multiplayer, also add NetworkToggle (and NetworkObject) to the same pivot.
// Sounds (creak on open, thud on close) are added automatically: see OpenCloseSound.
public class Door : MonoBehaviour, IInteractable
{
    [SerializeField] private float openAngle = 90f;   // use -90 to swing the other way
    [SerializeField] private float speed = 3f;
    [SerializeField] private bool isOpen = false;

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private NetworkToggle networkToggle;
    private OpenCloseSound sound;
    private bool wasNetworked;

    private void Awake()
    {
        closedRotation = transform.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);

        networkToggle = GetComponent<NetworkToggle>();
        if (networkToggle) networkToggle.SetStartOpen(isOpen);

        // Creak on open, thud when shut. Picks door or cabinet sounds by size.
        sound = OpenCloseSound.Ensure(gameObject, OpenCloseSound.Size.Auto);
    }

    public void Interact()
    {
        // Online: ask the network to flip it, so both players see the same door.
        if (networkToggle && networkToggle.IsReady) networkToggle.RequestToggle();
        else isOpen = !isOpen;
    }

    private void Update()
    {
        // Online, follow the shared value; each headset still animates the swing itself.
        bool networked = networkToggle && networkToggle.IsReady;
        if (networked)
        {
            isOpen = networkToggle.IsOpen;
            // The first shared value after joining isn't someone opening the door, so no sound.
            if (!wasNetworked) sound.SyncSilently(isOpen);
        }
        wasNetworked = networked;

        Quaternion target = isOpen ? openRotation : closedRotation;
        transform.localRotation = Quaternion.Slerp(
            transform.localRotation, target, Time.deltaTime * speed);

        // Each headset plays the sound when it sees the state change, so both players hear it.
        sound.Tick(isOpen, Quaternion.Angle(transform.localRotation, closedRotation));
    }
}
