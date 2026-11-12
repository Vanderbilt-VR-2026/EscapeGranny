using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Lets the knife and axe hurt the Prisoner: a hit counts when the held weapon touches the
/// Prisoner while swinging faster than Min Swing Speed, at most once per Cooldown.
/// Only the headset holding the weapon checks hits (the other player just sees it move).
/// Balance (set in the Inspector):
///   Knife: Damage 1, Min Swing Speed 1.5, Cooldown 0.4 (quick, needs two hits)
///   Axe:   Damage 2, Min Swing Speed 2.5, Cooldown 1.5 (one-hit kill, needs a real swing, slow to repeat)
/// Setup: put this on Knife_Root and Axe_Root (next to XRGrabInteractable and the Rigidbody).
/// </summary>
[RequireComponent(typeof(XRGrabInteractable), typeof(Rigidbody))]
public class MeleeWeapon : MonoBehaviour
{
    [Tooltip("Hits taken off the Prisoner (the Prisoner has 2).")]
    [SerializeField, Min(1)] private int damage = 1;
    [Tooltip("How fast the weapon must be moving to count as a hit (m/s). Higher = needs a bigger swing.")]
    [SerializeField, Min(0f)] private float minSwingSpeed = 1.5f;
    [Tooltip("Seconds before this weapon can hit again.")]
    [SerializeField, Min(0f)] private float cooldown = 0.4f;

    private XRGrabInteractable grab;
    private Vector3 lastPosition;
    private float speed;
    private float lastHitTime = -999f;

    private void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        lastPosition = transform.position;
    }

    // Measured from movement, because a held weapon's Rigidbody often reports no velocity.
    private void FixedUpdate()
    {
        speed = (transform.position - lastPosition).magnitude / Time.fixedDeltaTime;
        lastPosition = transform.position;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!grab.isSelected || speed < minSwingSpeed || Time.time - lastHitTime < cooldown) return;
        if (PlayerHitbox.TryHit(other, damage))
        {
            lastHitTime = Time.time;
            Debug.Log($"[MeleeWeapon] {name} hit the Prisoner at {speed:0.0} m/s");
        }
    }
}
