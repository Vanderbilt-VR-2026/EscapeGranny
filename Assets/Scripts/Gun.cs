using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Put on Pistol_Root. Trigger press (Activate) fires a bullet from the Muzzle
/// and plays a muzzle flash.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class Gun : MonoBehaviour
{
    [SerializeField] Transform muzzle;
    [SerializeField] GameObject bulletPrefab;
    [SerializeField] GameObject muzzleFlashPrefab;
    [SerializeField] float bulletSpeed = 25f;
    [SerializeField] float fireCooldown = 0.2f;

    XRGrabInteractable grab;
    Collider[] gunColliders;
    float lastFire = -999f;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        gunColliders = GetComponentsInChildren<Collider>();
    }

    void OnEnable() => grab.activated.AddListener(OnActivated);
    void OnDisable() => grab.activated.RemoveListener(OnActivated);

    void OnActivated(ActivateEventArgs args)
    {
        if (Time.time - lastFire < fireCooldown) return;
        lastFire = Time.time;
        Fire();
    }

    // Right-click the component header in Play mode and choose "Fire" to test without a headset.
    [ContextMenu("Fire")]
    void Fire()
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

        if (muzzleFlashPrefab != null)
        {
            var flash = Instantiate(muzzleFlashPrefab, muzzle);
            flash.transform.localPosition = Vector3.zero;
            flash.transform.localRotation = Quaternion.identity;
        }
    }
}
