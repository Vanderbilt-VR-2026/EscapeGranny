using UnityEngine;

// Put this on the LID PIVOT (an empty object at the hinge), not on the lid mesh.
// Uses the IInteractable interface defined in Door.cs, so keep Door.cs in the project.
public class Lid : MonoBehaviour, IInteractable
{
    [SerializeField] private Vector3 openAxis = Vector3.right; // X axis suits a chest hinged at the back
    [SerializeField] private float openAngle = -100f;          // flip the sign if it opens the wrong way
    [SerializeField] private float speed = 3f;
    [SerializeField] private bool isOpen = false;

    private Quaternion closedRotation;

    private void Awake()
    {
        closedRotation = transform.localRotation;
    }

    public void Interact()
    {
        isOpen = !isOpen;
    }

    private void Update()
    {
        // Read the angle every frame so Inspector changes apply immediately.
        Quaternion target = isOpen
            ? closedRotation * Quaternion.AngleAxis(openAngle, openAxis)
            : closedRotation;

        transform.localRotation = Quaternion.Slerp(
            transform.localRotation, target, Time.deltaTime * speed);
    }
}
