using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Changes how fast the player walks while holding this weapon, so each weapon has a trade-off:
/// a light knife lets Granny chase faster, a heavy axe slows her down.
/// Suggested: Knife 1.15, Axe 0.85, Gun 1, Bomb 1.
/// Setup: put this on each weapon root (Knife_Root, Axe_Root, Pistol_Root, Bomb_Root).
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class WeaponCarrySpeed : MonoBehaviour
{
    [Tooltip("Walk speed while holding this, compared to normal (1 = no change).")]
    [SerializeField, Range(0.3f, 1.5f)] private float speedMultiplier = 1f;

    private XRGrabInteractable grab;

    private void Awake() => grab = GetComponent<XRGrabInteractable>();

    // XRI only fires these on the headset that grabs, so only the holder slows down.
    private void OnEnable()
    {
        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnReleased);
    }

    private void OnDisable()
    {
        grab.selectEntered.RemoveListener(OnGrabbed);
        grab.selectExited.RemoveListener(OnReleased);
    }

    private void OnGrabbed(SelectEnterEventArgs args) => PlayerSpeed.SetWeapon(speedMultiplier);

    private void OnReleased(SelectExitEventArgs args)
    {
        if (!grab.isSelected) PlayerSpeed.SetWeapon(1f); // still held by the other hand: keep it
    }
}
