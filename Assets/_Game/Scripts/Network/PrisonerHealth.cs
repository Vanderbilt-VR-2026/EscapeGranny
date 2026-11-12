using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The Prisoner's health, shared by both players. Two hits by default (like Dead by Daylight):
/// the first leaves the Prisoner injured and slower, the second kills, which ends the round
/// ("GRANNY WINS" on both headsets, then everyone goes back to the Lobby for the next round).
/// Hiding (PlayerHideState) makes the Prisoner safe from hits.
/// Weapons hit an invisible capsule this script keeps on the Prisoner's body (PlayerHitbox).
/// The attacker's headset detects the hit and asks the Prisoner's headset (the owner) to lower the
/// health, so a hit only counts once.
/// Setup: put this on the Network Player prefab root, next to NetworkAvatarBody.
/// </summary>
[RequireComponent(typeof(NetworkObject), typeof(NetworkAvatarBody))]
public class PrisonerHealth : NetworkBehaviour
{
    [Tooltip("Hits the Prisoner can take. The last one kills.")]
    [SerializeField, Min(1)] private int maxHealth = 2;
    [Tooltip("Walk speed while injured, compared to normal (0.8 = 20% slower).")]
    [SerializeField, Range(0.1f, 1f)] private float injuredSpeed = 0.8f;
    [Tooltip("Size of the body that weapons can hit (m).")]
    [SerializeField] private float hitboxHeight = 1.7f;
    [SerializeField] private float hitboxRadius = 0.3f;
    [Tooltip("Seconds the GRANNY WINS message shows before everyone goes back to the Lobby.")]
    [SerializeField, Min(0f)] private float endRoundDelay = 5f;

    // How many hits the Prisoner has taken. Fusion copies it to the other player.
    [Networked, OnChangedRender(nameof(OnDamageChanged))]
    public int Damage { get; private set; }

    public bool IsDead => Damage >= maxHealth;
    public bool IsInjured => Damage > 0 && !IsDead;

    private NetworkAvatarBody avatar;
    private PlayerHideState hideState;
    private CapsuleCollider hitbox;
    private bool roundEnding;

    public override void Spawned()
    {
        avatar = GetComponent<NetworkAvatarBody>();
        hideState = GetComponent<PlayerHideState>();

        var go = new GameObject("Prisoner Hitbox");
        go.transform.SetParent(transform, false);
        hitbox = go.AddComponent<CapsuleCollider>();
        hitbox.isTrigger = true;
        hitbox.height = hitboxHeight;
        hitbox.radius = hitboxRadius;
        go.AddComponent<PlayerHitbox>().Health = this;

        if (HasStateAuthority) PlayerSpeed.SetInjured(1f);
    }

    // Called by weapons on the attacker's headset. Ignored on the Prisoner's own headset, so the
    // Prisoner can't hurt themselves by waving a weapon around.
    public void TakeHit(int amount)
    {
        if (!Object || !Object.IsValid || HasStateAuthority || IsDead) return;
        RpcTakeHit(amount);
    }

    // Runs on the Prisoner's headset, which owns the health.
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RpcTakeHit(int amount)
    {
        if (IsDead || avatar.Role != PlayerRole.Prisoner) return;
        if (hideState != null && hideState.IsHidden) return;
        Damage = Mathf.Min(maxHealth, Damage + amount);
    }

    private void OnDamageChanged()
    {
        Debug.Log($"[PrisonerHealth] Prisoner hit: {Damage}/{maxHealth}{(IsDead ? ", dead" : "")}");
        if (HasStateAuthority) PlayerSpeed.SetInjured(IsInjured ? injuredSpeed : 1f);

        if (IsDead && !roundEnding)
        {
            roundEnding = true;
            ShowMessage("GRANNY WINS");
            StartCoroutine(EndRound());
        }
    }

    // Every frame: keep the hitbox on the Prisoner's body, and only switch it on while it can be hit.
    public override void Render()
    {
        if (hitbox == null || avatar == null) return;
        bool hittable = avatar.Role == PlayerRole.Prisoner && !IsDead && !(hideState != null && hideState.IsHidden);
        hitbox.enabled = hittable;
        if (hittable)
            hitbox.transform.position = avatar.FeetPosition + Vector3.up * (hitboxHeight / 2f);
    }

    private IEnumerator EndRound()
    {
        yield return new WaitForSeconds(endRoundDelay);
        // Only one player may change the scene for both; the master client does it, like the lobby does.
        var lobby = FusionPlayerSpawnerAndLobbyController.Instance;
        if (Runner != null && Runner.IsSharedModeMasterClient && lobby != null)
            Runner.LoadScene(SceneRef.FromIndex(lobby.LobbySceneBuildIndex), LoadSceneMode.Single);
    }

    // Big red text floating in front of this headset's camera. Built in code so there's nothing to set up.
    private static void ShowMessage(string text)
    {
        var cam = Camera.main;
        if (cam == null) return;
        var go = new GameObject("Round Over Message");
        go.transform.SetParent(cam.transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, 1.5f);
        var mesh = go.AddComponent<TextMesh>();
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        mesh.font = font;
        go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        mesh.text = text;
        mesh.fontSize = 100;
        mesh.characterSize = 0.01f;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.color = Color.red;
    }
}
