
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Attach this to the same object as the XRSimpleInteractable.
public class HidingSpotPrompt : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private XRSimpleInteractable interactable;
    [SerializeField] private HidingSpot spot;
    [SerializeField] private GameObject promptRoot;

    private bool hovered;

    private void OnEnable()
    {
        if (interactable == null)
        {
            Debug.LogError("Hide prompt: Interactable is missing.");
            return;
        }

        interactable.hoverEntered.AddListener(OnHoverEnter);
        interactable.hoverExited.AddListener(OnHoverExit);
        interactable.selectEntered.AddListener(OnSelect);

        if (promptRoot != null)
            promptRoot.SetActive(false);
    }

    private void OnDisable()
    {
        if (interactable == null)
            return;

        interactable.hoverEntered.RemoveListener(OnHoverEnter);
        interactable.hoverExited.RemoveListener(OnHoverExit);
        interactable.selectEntered.RemoveListener(OnSelect);
    }

    private void OnHoverEnter(HoverEnterEventArgs args)
    {
        hovered = true;
    }

    private void OnHoverExit(HoverExitEventArgs args)
    {
        hovered = false;
    }

    private void OnSelect(SelectEnterEventArgs args)
    {
        hovered = false;

        if (spot == null)
        {
            Debug.LogError("Hide prompt: HidingSpot is missing.");
            return;
        }

        // Make sure the bed is not already occupied.
        if (!spot.TryOccupy())
        {
            Debug.Log("This hiding spot is occupied.");
            return;
        }

        // Find the local VR rig.
        if (HidingSpot.LocalRig == null)
        {
            Debug.LogError("LocalPlayerRig was not found.");
            spot.Release();
            return;
        }

        HidingSpot.LocalRig.EnterHiding(spot.HidePoint, spot);
    
    }

    private void Update()
    {
        if (spot == null || promptRoot == null)
            return;

        bool show = hovered && !spot.Occupied;

        if (promptRoot.activeSelf != show)
            promptRoot.SetActive(show);

        // Make the prompt face the headset camera.
        if (show && Camera.main != null)
        {
            Vector3 toCam =
                promptRoot.transform.position -
                Camera.main.transform.position;

            toCam.y = 0f;

            if (toCam.sqrMagnitude > 0.001f)
            {
                promptRoot.transform.rotation =
                    Quaternion.LookRotation(toCam);
            }
        }
    }

    // Call this when the player exits the hiding spot.
    public void ExitHiding()
    {
        if (spot == null)
            return;

        if (HidingSpot.LocalRig != null)
        {
            HidingSpot.LocalRig.ExitHiding(spot.ExitPoint);
        }

        if (PlayerHideState.LocalPlayer != null)
        {
            PlayerHideState.LocalPlayer.RequestUnhide();
        }

        spot.Release();
    }
}
