// OfflineGameLauncher.cs
//
// Single-headset test path: loads the house straight from the main menu, without Photon, and puts
// the player at a random spawn marker for the role they picked (G1-G3 for Granny, P1-P3 for the
// Prisoner). The player's HEAD goes on the marker, not just the rig, so standing off-centre in your
// play area can't put you inside a wall or under the floor. Lets you test menu -> house on one Quest, with no second player and no Fusion room.
// If Granny picked a weapon on the loadout screen, it's put straight into her right hand
// (see GrannyWeaponHolder). Both roles get the round countdown at the top of their view (RoundTimer).
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

        // No Network Player offline, so put footsteps on the headset camera instead.
        var head = GameObject.Find(RigName + "/Camera Offset/Main Camera");
        if (head != null) Footsteps.AddTo(head);

        // Both roles get the 5:00 countdown at the top of their view.
        RoundTimer.StartRound(RoundTimer.DefaultRoundSeconds);

        StartCoroutine(PlaceThenFinish());
    }

    // Seconds to wait for the headset to report tracking before placing the head anyway (Editor, no headset).
    private const float MaxTrackingWait = 2.5f;

    private IEnumerator PlaceThenFinish()
    {
        var rig = GameObject.Find(RigName);
        var spawns = FindSpawns(role == PlayerRole.Granny ? GrannySpawnsGroup : PrisonerSpawnsGroup);
        if (rig == null || spawns.Count == 0)
        {
            Debug.LogWarning($"[OfflineGameLauncher] Couldn't find '{RigName}' or any {role} spawn markers; leaving the player where the scene put them.");
        }
        else
        {
            var spawn = spawns[Random.Range(0, spawns.Count)];
            // Rough move straight away, so the first frames aren't somewhere random.
            MoveRig(rig, spawn.position, spawn.rotation);

            // The rig is the centre of the player's real play area, but the player can be standing a
            // metre or two away from it. Moving only the rig can then leave their head and body
            // outside the room (e.g. behind the basement wall, stuck under the floor). So once the
            // headset is tracking, slide the rig so the HEAD is on the spawn marker.
            var cam = rig.GetComponentInChildren<Camera>();
            if (cam != null)
            {
                int trackedFrames = 0;
                float waited = 0f;
                while (trackedFrames < 2 && waited < MaxTrackingWait)
                {
                    yield return null;
                    trackedFrames = HeadsetMenuPlacer.IsHeadsetTracked() ? trackedFrames + 1 : 0;
                    waited += Time.unscaledDeltaTime;
                }
                PutHeadOn(rig, cam.transform, spawn.position, spawn.eulerAngles.y);
            }
            Debug.Log($"[OfflineGameLauncher] {role} spawned at {spawn.name}");
        }

        // Granny's weapon goes into her hand once she's in place (the hands follow the moved rig).
        if (role == PlayerRole.Granny && weapon != WeaponType.None)
        {
            yield return null;
            GrannyWeaponHolder.Equip(weapon);
        }
        Destroy(gameObject);
    }

    // Turns the rig around the head so the player faces `yaw`, then slides it sideways so the head is
    // straight above `spot`. Height isn't touched: the rig keeps standing on the spawn's floor.
    public static void PutHeadOn(GameObject rig, Transform head, Vector3 spot, float yaw)
    {
        var body = rig.GetComponent<CharacterController>();
        if (body != null) body.enabled = false;
        rig.transform.RotateAround(head.position, Vector3.up, Mathf.DeltaAngle(head.eulerAngles.y, yaw));
        rig.transform.position += new Vector3(spot.x - head.position.x, 0f, spot.z - head.position.z);
        if (body != null) ReEnableBody(body);
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