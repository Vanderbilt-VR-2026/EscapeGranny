// OfflineGameLauncher.cs
//
// Single-headset test path: loads the house straight from the main menu, without Photon, and puts
// the player at a random spawn marker for the role they picked (G1-G3 for Granny, P1-P3 for the
// Prisoner). Lets you test menu -> house on one Quest, with no second player and no Fusion room.
// If Granny picked a weapon on the loadout screen, it's put straight into her right hand
// (see GrannyWeaponHolder).
//
// Not the real multiplayer flow: that starts from the Lobby scene. Networked objects in the house
// have no runner here, so anything that needs Fusion (e.g. network grabbing) may not work.
//
// Setup: nothing to put in a scene. MenuController.StartGame() calls OfflineGameLauncher.Launch().
// The house scene must be in the build's scene list.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class OfflineGameLauncher : MonoBehaviour
{
    // Names HouseBuilder gives the marker groups under each floor's "Gameplay Markers".
    private const string GrannySpawnsGroup = "Granny Spawns";
    private const string PrisonerSpawnsGroup = "Prisoner Spawns";
    private const string RigName = "XR Origin (XR Rig)";

    private PlayerRole role;
    private WeaponType weapon = WeaponType.None;

    public static void Launch(string sceneName, PlayerRole role)
    {
        // Lives across the scene change just long enough to place the player, then removes itself.
        var launcher = new GameObject("OfflineGameLauncher").AddComponent<OfflineGameLauncher>();
        launcher.role = role;

        // Remember Granny's weapon now, while the menu's GameSession definitely still exists.
        if (role == PlayerRole.Granny && GameSession.Instance != null)
            launcher.weapon = GameSession.Instance.SelectedWeapon;

        DontDestroyOnLoad(launcher.gameObject);
        SceneManager.sceneLoaded += launcher.OnSceneLoaded;
        SceneManager.LoadScene(sceneName);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        MoveRigToSpawn();

        if (role == PlayerRole.Granny && weapon != WeaponType.None)
            StartCoroutine(EquipWeaponThenFinish());
        else
            Destroy(gameObject);
    }

    private IEnumerator EquipWeaponThenFinish()
    {
        // Give the XR rig's hands a couple of frames to start up after the move.
        yield return null;
        yield return null;

        GrannyWeaponHolder.Equip(weapon);
        Destroy(gameObject);
    }

    private void MoveRigToSpawn()
    {
        var rig = GameObject.Find(RigName);
        var spawns = FindSpawns(role == PlayerRole.Granny ? GrannySpawnsGroup : PrisonerSpawnsGroup);
        if (rig == null || spawns.Count == 0)
        {
            Debug.LogWarning($"[OfflineGameLauncher] Couldn't find '{RigName}' or any {role} spawn markers; leaving the player where the scene put them.");
            return;
        }

        var spawn = spawns[Random.Range(0, spawns.Count)];
        MoveRig(rig, spawn.position, spawn.rotation);
        Debug.Log($"[OfflineGameLauncher] {role} spawned at {spawn.name}");
    }

    // Teleports the XR rig. Also used by MenuRoomPicker.
    public static void MoveRig(GameObject rig, Vector3 position, Quaternion rotation)
    {
        // A CharacterController ignores direct position changes while enabled, so switch it off for the move.
        var body = rig.GetComponent<CharacterController>();
        if (body != null) body.enabled = false;
        rig.transform.SetPositionAndRotation(position, rotation);
        if (body != null) ReEnableBody(body);
    }

    // Switches a CharacterController back on. Unity refuses (and leaves it off for good, logging
    // "Step Offset must be less or equal to..." and then "Move called on inactive controller" every
    // frame) if its height is too small for its Step Offset, which happens before the headset's
    // height arrives. So make the height valid first; the XR rig sets the real height next frame.
    // Also used by HeadsetMenuPlacer.
    public static void ReEnableBody(CharacterController body)
    {
        float minHeight = body.stepOffset - 2f * body.radius + 0.01f;
        if (body.height < minHeight)
        {
            body.height = minHeight;
            var center = body.center;
            center.y = Mathf.Max(center.y, minHeight / 2f + body.skinWidth);
            body.center = center;
        }
        body.enabled = true;
    }

    // Every child of every group with this name (each floor has its own group). Also used by MenuRoomPicker.
    public static List<Transform> FindSpawns(string groupName)
    {
        var spawns = new List<Transform>();
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t.name == groupName)
                foreach (Transform child in t) spawns.Add(child);
        return spawns;
    }
}
