// GrannyWeaponHolder.cs
//
// Puts Granny's chosen weapon (from the main menu's WeaponLoadout) into her right hand when she
// spawns in the house. OfflineGameLauncher calls GrannyWeaponHolder.Equip(...) after placing her.
//
// It's held for her only until she first squeezes the grip button, so it doesn't fall the moment
// she spawns. From then on it's a normal grab, like any real item: she has to keep holding grip,
// letting go drops (or throws) it, and she has to pick it back up.
//
// Setup: nothing to put in a scene. The weapons must exist in the house with these names.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class GrannyWeaponHolder : MonoBehaviour
{
    // Object names of the weapons in House_Bishal.
    private static readonly Dictionary<WeaponType, string> WeaponObjectNames = new Dictionary<WeaponType, string>
    {
        { WeaponType.Axe, "Axe_Root" },
        { WeaponType.Knife, "Knife_Root" },
        { WeaponType.Gun, "Pistol_Root" },
        { WeaponType.Bomb, "Bomb_Root" },
    };

    private const string RightHandPath = "XR Origin (XR Rig)/Camera Offset/Right Controller";

    private XRBaseInputInteractor hand;

    /// <summary>Puts the weapon in the right hand. Returns false (and logs why) if it couldn't.</summary>
    public static bool Equip(WeaponType weapon)
    {
        if (!WeaponObjectNames.TryGetValue(weapon, out var objectName))
        {
            Debug.LogWarning($"[GrannyWeaponHolder] No weapon object is set up for '{weapon}'.");
            return false;
        }

        var weaponObject = FindInScene(objectName);
        if (weaponObject == null)
        {
            Debug.LogWarning($"[GrannyWeaponHolder] Couldn't find '{objectName}' in the scene.");
            return false;
        }
        weaponObject.SetActive(true);

        var grab = weaponObject.GetComponentInChildren<XRGrabInteractable>(true);
        if (grab == null)
        {
            Debug.LogWarning($"[GrannyWeaponHolder] '{objectName}' has no XR Grab Interactable.", weaponObject);
            return false;
        }

        var hand = FindRightHandInteractor();
        if (hand == null)
        {
            Debug.LogWarning("[GrannyWeaponHolder] Couldn't find a grab interactor on the right controller.");
            return false;
        }

        // Bring the weapon to the hand first, so it doesn't fly across the house when grabbed.
        var target = hand.GetAttachTransform(grab);
        var rb = grab.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        grab.transform.SetPositionAndRotation(target.position, target.rotation);
        if (rb != null)
        {
            rb.position = target.position;
            rb.rotation = target.rotation;
        }

        // Hold it for her until she squeezes grip herself (see Update).
        hand.StartManualInteraction((IXRSelectInteractable)grab);

        var holder = hand.gameObject.AddComponent<GrannyWeaponHolder>();
        holder.hand = hand;

        Debug.Log($"[GrannyWeaponHolder] Granny is holding the {weapon} ({objectName}).");
        return true;
    }

    private void Update()
    {
        if (hand == null || !hand.isPerformingManualInteraction)
        {
            Destroy(this);
            return;
        }

        // Once she squeezes grip, hand it over to a normal grab: while she keeps holding grip she
        // keeps the weapon, and letting go drops it like any other item.
        if (hand.selectInput.ReadIsPerformed())
        {
            hand.EndManualInteraction();
            Destroy(this);
        }
    }

    // The right hand's grab interactor (Near-Far or Direct), not the teleport ray.
    private static XRBaseInputInteractor FindRightHandInteractor()
    {
        var right = GameObject.Find(RightHandPath);
        if (right == null) return null;

        var interactors = right.GetComponentsInChildren<XRBaseInputInteractor>(false)
            .Where(i => i.isActiveAndEnabled && !i.name.ToLower().Contains("teleport"))
            .ToList();

        return interactors.FirstOrDefault(i => i.GetType().Name == "NearFarInteractor")
               ?? interactors.FirstOrDefault(i => i is XRDirectInteractor)
               ?? interactors.FirstOrDefault();
    }

    // Finds an object by name in the loaded scenes, including inactive ones.
    private static GameObject FindInScene(string objectName)
    {
        return FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(t => t.name == objectName && t.gameObject.scene.IsValid())
            .Select(t => t.gameObject)
            .FirstOrDefault();
    }
}
