using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Put on Pistol_Root. Trigger press (Activate) fires a bullet from the Muzzle
/// and plays a muzzle flash and a gunshot sound (heard by both players when NetworkGun is on it).
/// Leave Shot Sound empty to use the placeholder from Tools > Escape Granny > Generate Weapon Sounds.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class Gun : MonoBehaviour
{
    [SerializeField] Transform muzzle;
    [SerializeField] GameObject bulletPrefab;
    [SerializeField] GameObject muzzleFlashPrefab;
    [SerializeField] float bulletSpeed = 25f;
    [SerializeField] float fireCooldown = 0.2f;

    [Header("Sound (empty = use Resources/WeaponSounds/GunShot)")]
    [SerializeField] AudioClip shotSound;
    [SerializeField, Range(0f, 1f)] float shotVolume = 1f;
    [Tooltip("Can't be heard beyond this many metres. The house is 18 x 14 m, so 40 reaches everywhere.")]
    [SerializeField] float shotHearingDistance = 40f;
    [Tooltip("Random pitch change per shot (0.05 = up to 5% higher or lower), so repeated shots don't sound copied.")]
    [SerializeField, Range(0f, 0.3f)] float shotPitchVariation = 0.05f;

    XRGrabInteractable grab;
    Collider[] gunColliders;
    NetworkGun networkGun;
    float lastFire = -999f;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        gunColliders = GetComponentsInChildren<Collider>();
        networkGun = GetComponent<NetworkGun>();

        // Placeholder from Tools > Escape Granny > Generate Weapon Sounds, if none is dragged in.
        if (shotSound == null) shotSound = Resources.Load<AudioClip>("WeaponSounds/GunShot");
        DropSound.Ensure(gameObject, "DropHeavy"); // clank when it lands on the floor
    }

    void OnEnable() => grab.activated.AddListener(OnActivated);
    void OnDisable() => grab.activated.RemoveListener(OnActivated);

    void OnActivated(ActivateEventArgs args)
    {
        if (Time.time - lastFire < fireCooldown) return;
        lastFire = Time.time;

        // Online: tell both players, so the other side sees the shot too.
        if (networkGun && networkGun.IsReady) networkGun.RequestFire();
        else Fire();
    }

    // Right-click the component header in Play mode and choose "Fire" to test without a headset.
    [ContextMenu("Fire")]
    public void Fire()
    {
        if (muzzle == null || bulletPrefab == null)
        {
            Debug.LogWarning("Gun: assign Muzzle and Bullet Prefab.");
            return;
        }

        var bullet = Instantiate(bulletPrefab, muzzle.position, muzzle.rotation);

        // Each shot gets its own sound object, so quick shots overlap instead of cutting each other off.
        OneShotSound.PlayAt(shotSound, muzzle.position, shotVolume, shotHearingDistance, 3f, shotPitchVariation);

        // Don't let the bullet collide with the gun that fired it.
        foreach (var bc in bullet.GetComponentsInChildren<Collider>())
            foreach (var gc in gunColliders)
                Physics.IgnoreCollision(bc, gc);

        if (bullet.TryGetComponent<Rigidbody>(out var rb))
            rb.linearVelocity = muzzle.forward * bulletSpeed;

        if (muzzleFlashPrefab != null)
        {
            var flash = Instantiate(muzzleFlashPrefab, muzzle);
            flash.transform.localPosition = Vector3.zero;
            flash.transform.localRotation = Quaternion.identity;
        }
    }
}
