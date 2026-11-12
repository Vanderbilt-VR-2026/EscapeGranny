using System;
using System.Collections;
using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The networked start menu: who is Granny, who is the Prisoner, Granny's weapon, and who pressed READY.
/// Flow: Lobby (2/2) → the lobby controller loads the start menu for both → the first player to claim a
/// role gets it (it greys out for the other) → Granny picks a weapon → both press READY → the master
/// loads the house → each player is placed at a random spawn for their role, and only Granny's chosen
/// weapon appears, in her hand, for both players to see.
/// The menu scripts don't talk to Fusion themselves: LocalRoleClaimService and MenuController use this
/// when it exists, and work offline as before when it doesn't (MainMenu opened on its own).
/// Setup: put this on the "Fusion Player Spawner and Lobby Controller" prefab (next to that script), and
/// set House Scene Build Index. Build list: 0 Lobby Scene, 1 MainMenu, 2 House_Bishal.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NetworkRoleSelect : NetworkBehaviour
{
    public static NetworkRoleSelect Instance { get; private set; }

    // Raised on every headset when a claim, the weapon or a READY changes, so the menu can refresh.
    public static event Action Changed;

    [Tooltip("Build index of the house (File > Build Profiles > Scene List).")]
    [SerializeField] private int houseSceneBuildIndex = 2;
    [Tooltip("Seconds to wait for the headset to report tracking before placing the player anyway.")]
    [SerializeField, Min(0f)] private float maxTrackingWait = 2.5f;

    [Networked, OnChangedRender(nameof(RaiseChanged))] public PlayerRef GrannyPlayer { get; private set; }
    [Networked, OnChangedRender(nameof(RaiseChanged))] public PlayerRef PrisonerPlayer { get; private set; }
    [Networked, OnChangedRender(nameof(RaiseChanged))] public WeaponType GrannyWeapon { get; private set; }
    [Networked, OnChangedRender(nameof(RaiseChanged))] public NetworkBool GrannyReady { get; private set; }
    [Networked, OnChangedRender(nameof(RaiseChanged))] public NetworkBool PrisonerReady { get; private set; }

    // True once Fusion has connected this object, so its values and RPCs can be used.
    public bool IsReady => Object && Object.IsValid;

    public PlayerRole RoleOf(PlayerRef player) =>
        player == PlayerRef.None ? PlayerRole.None :
        player == GrannyPlayer ? PlayerRole.Granny :
        player == PrisonerPlayer ? PlayerRole.Prisoner : PlayerRole.None;

    // True if a player OTHER than this headset's holds the role.
    public bool IsTakenByOther(PlayerRole role)
    {
        var holder = role == PlayerRole.Granny ? GrannyPlayer : role == PlayerRole.Prisoner ? PrisonerPlayer : PlayerRef.None;
        return holder != PlayerRef.None && holder != Runner.LocalPlayer;
    }

    private NetworkEvents events;
    private bool loadingScene;

    public override void Spawned()
    {
        Instance = this;
        events = Runner.GetComponent<NetworkEvents>();
        events.OnSceneLoadDone.AddListener(SceneLoaded);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this) Instance = null;
        if (events) events.OnSceneLoadDone.RemoveListener(SceneLoaded);
    }

    private void RaiseChanged() => Changed?.Invoke();

    // ---- Called by the menu on this headset ----

    // PlayerRole.None gives up this player's role.
    public void RequestClaim(PlayerRole role) => RpcClaim(Runner.LocalPlayer, role);

    public void RequestReady(PlayerRole role, WeaponType weapon) => RpcReady(Runner.LocalPlayer, role, weapon);

#if UNITY_EDITOR
    // Testing alone with a headset + the Editor: the Editor player has no controllers to click the VR
    // menu, so in the start menu press P (Prisoner, ready) or G (Granny with the gun, ready) instead.
    // 1-4 also make the Editor Granny with the Axe, Knife, Gun or Bomb.
    private void Update()
    {
        var keys = UnityEngine.InputSystem.Keyboard.current;
        var lobby = FusionPlayerSpawnerAndLobbyController.Instance;
        if (keys == null || !IsReady || lobby == null || SceneManager.GetActiveScene().buildIndex != lobby.GameSceneBuildIndex) return;

        if (keys.pKey.wasPressedThisFrame) EditorPick(PlayerRole.Prisoner, WeaponType.None);
        else if (keys.gKey.wasPressedThisFrame || keys.digit3Key.wasPressedThisFrame) EditorPick(PlayerRole.Granny, WeaponType.Gun);
        else if (keys.digit1Key.wasPressedThisFrame) EditorPick(PlayerRole.Granny, WeaponType.Axe);
        else if (keys.digit2Key.wasPressedThisFrame) EditorPick(PlayerRole.Granny, WeaponType.Knife);
        else if (keys.digit4Key.wasPressedThisFrame) EditorPick(PlayerRole.Granny, WeaponType.Bomb);
    }

    private void EditorPick(PlayerRole role, WeaponType weapon)
    {
        if (IsTakenByOther(role)) { Debug.Log($"[NetworkRoleSelect] {role} is already taken."); return; }
        if (GameSession.Instance != null)
        {
            GameSession.Instance.SelectedRole = role;
            GameSession.Instance.SelectedWeapon = weapon;
        }
        RequestClaim(role);
        RequestReady(role, weapon);
        FloatingMessage.Show($"Editor: {role} {(weapon != WeaponType.None ? weapon.ToString() : "")}, ready", Color.white);
    }
#endif

    // ---- Run on the master (state authority), who decides for both ----

    // First come, first served: a role already held by the other player is refused.
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RpcClaim(PlayerRef player, PlayerRole role)
    {
        if (role == PlayerRole.Granny && GrannyPlayer != PlayerRef.None && GrannyPlayer != player) return;
        if (role == PlayerRole.Prisoner && PrisonerPlayer != PlayerRef.None && PrisonerPlayer != player) return;

        // Changing roles: let go of the old one first.
        if (GrannyPlayer == player) { GrannyPlayer = PlayerRef.None; GrannyReady = false; GrannyWeapon = WeaponType.None; }
        if (PrisonerPlayer == player) { PrisonerPlayer = PlayerRef.None; PrisonerReady = false; }

        if (role == PlayerRole.Granny) GrannyPlayer = player;
        if (role == PlayerRole.Prisoner) PrisonerPlayer = player;
        Debug.Log($"[NetworkRoleSelect] {player} claimed {role}");
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RpcReady(PlayerRef player, PlayerRole role, WeaponType weapon)
    {
        if (role == PlayerRole.Granny && GrannyPlayer == player)
        {
            GrannyWeapon = weapon;
            GrannyReady = true;
        }
        else if (role == PlayerRole.Prisoner && PrisonerPlayer == player)
        {
            PrisonerReady = true;
        }
        else
        {
            Debug.LogWarning($"[NetworkRoleSelect] {player} pressed READY as {role} but doesn't hold that role.");
        }
    }

    // The master starts the round once both are ready, and ends it if a player leaves the house.
    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || loadingScene || Runner.IsSceneManagerBusy) return;
        var lobby = FusionPlayerSpawnerAndLobbyController.Instance;
        if (lobby == null) return;
        int scene = SceneManager.GetActiveScene().buildIndex;

        if (scene == lobby.GameSceneBuildIndex && GrannyReady && PrisonerReady)
            LoadScene(houseSceneBuildIndex);
        else if (scene == houseSceneBuildIndex && Runner.ActivePlayers.Count() < 2)
            LoadScene(lobby.LobbySceneBuildIndex);
    }

    private void LoadScene(int buildIndex)
    {
        loadingScene = true;
        Runner.LoadScene(SceneRef.FromIndex(buildIndex), LoadSceneMode.Single);
    }

    private void SceneLoaded(NetworkRunner runner)
    {
        loadingScene = false;
        var lobby = FusionPlayerSpawnerAndLobbyController.Instance;
        int scene = SceneManager.GetActiveScene().buildIndex;

        // Back in the Lobby: a new round, so everyone picks again.
        if (HasStateAuthority && lobby != null && scene == lobby.LobbySceneBuildIndex)
        {
            GrannyPlayer = PrisonerPlayer = PlayerRef.None;
            GrannyWeapon = WeaponType.None;
            GrannyReady = PrisonerReady = false;
        }

        if (scene == houseSceneBuildIndex) StartCoroutine(PlaceLocalPlayer());
    }

    // In the house: put this headset's player at a random spawn for their role, then show Granny's weapon.
    private IEnumerator PlaceLocalPlayer()
    {
        // The lobby controller moves the rig while spawning our avatar; wait for that so we move it last.
        float waited = 0f;
        while (Runner.GetPlayerObject(Runner.LocalPlayer) == null && waited < 5f)
        {
            yield return null;
            waited += Time.unscaledDeltaTime;
        }

        var role = RoleOf(Runner.LocalPlayer);
        var rig = GameObject.Find("XR Origin (XR Rig)");
        var spawns = OfflineGameLauncher.FindSpawns(role == PlayerRole.Granny ? "Granny Spawns" : "Prisoner Spawns");
        if (rig != null && spawns.Count > 0 && role != PlayerRole.None)
        {
            var spawn = spawns[UnityEngine.Random.Range(0, spawns.Count)];
            OfflineGameLauncher.MoveRig(rig, spawn.position, spawn.rotation);

            // The player may stand away from the centre of their play area, so put the HEAD on the spot
            // once the headset is tracking (same as the offline launcher).
            var cam = rig.GetComponentInChildren<Camera>();
            if (cam != null)
            {
                int trackedFrames = 0;
                waited = 0f;
                while (trackedFrames < 2 && waited < maxTrackingWait)
                {
                    yield return null;
                    trackedFrames = HeadsetMenuPlacer.IsHeadsetTracked() ? trackedFrames + 1 : 0;
                    waited += Time.unscaledDeltaTime;
                }
                OfflineGameLauncher.PutHeadOn(rig, cam.transform, spawn.position, spawn.eulerAngles.y);
            }
            Debug.Log($"[NetworkRoleSelect] {role} placed at {spawn.name}");
        }

        RoundTimer.StartRound(RoundTimer.DefaultRoundSeconds);

        // Only Granny's chosen weapon exists in the house; both players switch it on so both see it.
        if (GrannyWeapon != WeaponType.None)
        {
            yield return null;
            if (role == PlayerRole.Granny) GrannyWeaponHolder.Equip(GrannyWeapon);
            else GrannyWeaponHolder.Show(GrannyWeapon);
        }
    }
}
