// OfflineGameLauncher.cs
//
// Single-headset test path: loads the house straight from the main menu, without Photon, and puts
// the player at a random spawn marker for the role they picked (G1-G3 for Granny, P1-P3 for the
// Prisoner). Lets you test menu -> house on one Quest, with no second player and no Fusion room.
//
// Not the real multiplayer flow: that starts from the Lobby scene. Networked objects in the house
// have no runner here, so anything that needs Fusion (e.g. network grabbing) may not work.
//
// Setup: nothing to put in a scene. MenuController.StartGame() calls OfflineGameLauncher.Launch().
// The house scene must be in the build's scene list.

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

    public static void Launch(string sceneName, PlayerRole role)
    {
        // Lives across the scene change just long enough to place the player, then removes itself.
        var launcher = new GameObject("OfflineGameLauncher").AddComponent<OfflineGameLauncher>();
        launcher.role = role;
        DontDestroyOnLoad(launcher.gameObject);
        SceneManager.sceneLoaded += launcher.OnSceneLoaded;
        SceneManager.LoadScene(sceneName);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        MoveRigToSpawn();
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
        if (body != null) body.enabled = true;
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
