// KeypadXR.cs
//
// Makes the Keypad FREE asset (Assets/Keypad) work in VR, so nobody has to wire 11 buttons by hand.
// When the scene starts it gives every KeypadButton an XR Simple Interactable: point the controller
// ray at a button and press grip, poke it with a controller or a tracked finger, or point a tracked
// hand and pinch. It also opens the given Lid when the right code is entered.
//
// Setup: put this on the Keypad (the object with the Keypad component) and drag the chest's LidPivot
// (the object with the Lid component, Locked ticked) into Lid. Set the code in Keypad's Keypad Combo.
// Don't also wire the buttons' Select Entered by hand, or each press would count twice.

using NavKeypad;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Keypad))]
public class KeypadXR : MonoBehaviour
{
    [Tooltip("The lid opened by the right code (LidPivot with the Lid component).")]
    [SerializeField] private Lid lid;
    [Tooltip("How much bigger each button's invisible hit area is than the button, so near misses with " +
             "the ray still count. Above 1.5 neighbouring buttons would overlap.")]
    [SerializeField, Range(1f, 1.5f)] private float hitAreaScale = 1.45f;
    [Tooltip("Presses closer together than this (seconds) are ignored, so a hand brushing across the " +
             "keypad doesn't type two digits at once.")]
    [SerializeField, Min(0f)] private float pressCooldown = 0.25f;

    private float lastPressTime = float.NegativeInfinity;

    private void Awake()
    {
        foreach (var button in GetComponentsInChildren<KeypadButton>(true))
        {
            // The asset's button colliders are triggers, which the XR ray ignores (it would pass
            // straight through to whatever is behind). Make them solid so the ray can hit them.
            foreach (var col in button.GetComponents<Collider>())
            {
                col.isTrigger = false;
                // Wider and taller, not deeper, so it doesn't stick out in front of the panel.
                if (col is BoxCollider box)
                    box.size = Vector3.Scale(box.size, new Vector3(hitAreaScale, hitAreaScale, 1f));
            }

            var interactable = button.GetComponent<XRSimpleInteractable>();
            if (interactable == null) interactable = button.gameObject.AddComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(_ =>
            {
                if (Time.time - lastPressTime < pressCooldown) return;
                lastPressTime = Time.time;
                button.PressButton();
            });
        }

        if (lid != null)
            GetComponent<Keypad>().OnAccessGranted.AddListener(() =>
            {
                Debug.Log($"[KeypadXR] Code accepted, opening {lid.name}.", lid);
                lid.UnlockAndOpen();
            });
        else Debug.LogWarning("[KeypadXR] No Lid set, so the right code won't open anything.", this);
    }
}
