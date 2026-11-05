using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables; // XRI 3.x. Remove this line on XRI 2.x.

// PUT THIS ON THE SAME OBJECT AS HidingSpot.
public class HidingSpotPrompt : MonoBehaviour
{
    [SerializeField] XRSimpleInteractable interactable;
    [SerializeField] HidingSpot spot;
    [SerializeField] GameObject promptRoot; // the "Hide" world-space canvas

    bool hovered;

    void OnEnable()
    {
        interactable.hoverEntered.AddListener(OnHoverEnter);
        interactable.hoverExited.AddListener(OnHoverExit);
        interactable.selectEntered.AddListener(OnSelect);
        promptRoot.SetActive(false);
    }

    void OnDisable()
    {
        interactable.hoverEntered.RemoveListener(OnHoverEnter);
        interactable.hoverExited.RemoveListener(OnHoverExit);
        interactable.selectEntered.RemoveListener(OnSelect);
    }

    void OnHoverEnter(HoverEnterEventArgs args)  => hovered = true;
    void OnHoverExit(HoverExitEventArgs args)    => hovered = false;
    void OnSelect(SelectEnterEventArgs args)     => hovered = false;

    void Update()
    {
        bool occupied = spot.Object != null && spot.Object.IsValid && spot.Occupied;
        bool show = hovered && !occupied;

        if (promptRoot.activeSelf != show)
            promptRoot.SetActive(show);

        if (show && Camera.main != null)
        {
            Vector3 toCam = promptRoot.transform.position - Camera.main.transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 0.001f)
                promptRoot.transform.rotation = Quaternion.LookRotation(toCam);
        }
    }
}
