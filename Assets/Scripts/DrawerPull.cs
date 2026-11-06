using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables; // XR Interaction Toolkit 3.x
using UnityEngine.XR.Interaction.Toolkit.Interactors;   // XR Interaction Toolkit 3.x
// If you are on XR Interaction Toolkit 2.x, delete the two "Interactables"/"Interactors" lines above.

// Put this on the DRAWER object, the same object that has the Collider and XR Simple Interactable.
// The drawer follows the controller while you hold the trigger and slides back along one axis only.
[RequireComponent(typeof(XRSimpleInteractable))]
public class DrawerPull : MonoBehaviour
{
    [SerializeField] private Vector3 slideAxis = Vector3.forward; // local direction the drawer pulls out
    [SerializeField] private float maxDistance = 0.4f;            // how far it can slide, in the parent's units
    [SerializeField] private bool snapShutWhenReleased = false;   // true = drawer slides closed on release

    private XRSimpleInteractable interactable;
    private Transform parent;
    private Vector3 axis;
    private Vector3 closedLocalPosition;
    private IXRSelectInteractor grabbingInteractor;
    private float grabOffset;
    private float currentSlide;

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        parent = transform.parent;
        axis = slideAxis.normalized;
        closedLocalPosition = transform.localPosition;
    }

    private void OnEnable()
    {
        interactable.selectEntered.AddListener(OnGrab);
        interactable.selectExited.AddListener(OnRelease);
    }

    private void OnDisable()
    {
        interactable.selectEntered.RemoveListener(OnGrab);
        interactable.selectExited.RemoveListener(OnRelease);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        grabbingInteractor = args.interactorObject;
        // Remember where along the axis the hand grabbed, so the drawer doesn't jump.
        grabOffset = HandDistanceAlongAxis() - currentSlide;
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        grabbingInteractor = null;
    }

    private void Update()
    {
        if (grabbingInteractor != null)
        {
            currentSlide = Mathf.Clamp(HandDistanceAlongAxis() - grabOffset, 0f, maxDistance);
        }
        else if (snapShutWhenReleased)
        {
            currentSlide = Mathf.MoveTowards(currentSlide, 0f, Time.deltaTime * 1f);
        }

        transform.localPosition = closedLocalPosition + axis * currentSlide;
    }

    // Distance the controller has moved along the slide axis, measured in the parent's space.
    private float HandDistanceAlongAxis()
    {
        Vector3 handWorld = grabbingInteractor.transform.position;
        Vector3 handLocal = parent != null ? parent.InverseTransformPoint(handWorld) : handWorld;
        return Vector3.Dot(handLocal - closedLocalPosition, axis);
    }
}
