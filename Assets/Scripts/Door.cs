using UnityEngine;

public interface IInteractable
{
    void Interact();
}

// Put this on the door's PIVOT object (the hinge), not the mesh itself.
public class Door : MonoBehaviour, IInteractable
{
    [SerializeField] private float openAngle = 90f;   // use -90 to swing the other way
    [SerializeField] private float speed = 3f;
    [SerializeField] private bool isOpen = false;

    private Quaternion closedRotation;
    private Quaternion openRotation;

    private void Awake()
    {
        closedRotation = transform.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
    }

    public void Interact()
    {
        isOpen = !isOpen;
    }

    private void Update()
    {
        Quaternion target = isOpen ? openRotation : closedRotation;
        transform.localRotation = Quaternion.Slerp(
            transform.localRotation, target, Time.deltaTime * speed);
    }
}
