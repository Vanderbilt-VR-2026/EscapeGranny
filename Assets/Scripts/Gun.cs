using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Put on Pistol_Root. Trigger press (Activate) fires a bullet from the Muzzle
/// and plays a muzzle flash.
/// Balance: long range, but only Ammo bullets per round and a slow Fire Cooldown, and the shot is
/// heard across the house. Each bullet takes Damage hits off the Prisoner.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class Gun : MonoBehaviour
{
    [SerializeField] Transform muzzle;
    [SerializeField] GameObject bulletPrefab;
    [SerializeField] GameObject muzzleFlashPrefab;
    [SerializeField] float bulletSpeed = 25f;
    [SerializeField] float fireCooldown = 0.2f;

    [Header("Balance")]
    [Tooltip("Bullets for the whole round.")]
    [SerializeField, Min(0)] int ammo = 3;
    [Tooltip("Hits each bullet takes off the Prisoner (the Prisoner has 2).")]
    [SerializeField, Min(1)] int damage = 1;
    [Tooltip("Played when the trigger is pressed with no bullets left.")]
    [SerializeField] AudioClip emptySound;

    XRGrabInteractable grab;
    Collider[] gunColliders;
    NetworkGun networkGun;
    float lastFire = -999f;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        gunColliders = GetComponentsInChildren<Collider>();
        networkGun = GetComponent<NetworkGun>();
    }

    void OnEnable() => grab.activated.AddListener(OnActivated);
    void OnDisable() => grab.activated.RemoveListener(OnActivated);

    void OnActivated(ActivateEventArgs args)
    {
        if (Time.time - lastFire < fireCooldown) return;
        lastFire = Time.time;

        if (ammo <= 0)
        {
            if (emptySound != null) AudioSource.PlayClipAtPoint(emptySound, transform.position);
            return;
        }
        ammo--;

        // Online: tell both players, so the other side sees the shot too.
        if (networkGun && networkGun.IsReady) networkGun.RequestFire();
        else Fire();
    }

    // Right-click the component header in Play mode and choose "Fire" to test without a headset.
    [ContextMenu("Fire")]
    public void Fire() => Fire(true);

    // Online, both headsets spawn a bullet so both see it; only the shooter's bullet does damage,
    // otherwise every hit would count twice.
    public void Fire(bool dealsDamage)
    {
        if (muzzle == null || bulletPrefab == null)
        {
            Debug.LogWarning("Gun: assign Muzzle and Bullet Prefab.");
            return;
        }

        var bullet = Instantiate(bulletPrefab, muzzle.position, muzzle.rotation);

        // Don't let the bullet collide with the gun that fired it.
        foreach (var bc in bullet.GetComponentsInChildren<Collider>())
            foreach (var gc in gunColliders)
                Physics.IgnoreCollision(bc, gc);

        if (bullet.TryGetComponent<Rigidbody>(out var rb))
            rb.linearVelocity = muzzle.forward * bulletSpeed;

        if (bullet.TryGetComponent<Bullet>(out var shot))
            shot.Damage = dealsDamage ? damage : 0;

        if (muzzleFlashPrefab != null)
        {
            var flash = Instantiate(muzzleFlashPrefab, muzzle);
            flash.transform.localPosition = Vector3.zero;
            flash.transform.localRotation = Quaternion.identity;
        }
    }
}
