// LocalRoleClaimService.cs
//
// Makes sure the two players end up on opposite sides on the role select screen.
//
// Online (started from the Lobby, so NetworkRoleSelect exists): claims go through the network. The
// first player to claim a role gets it, and it greys out on the other headset.
// Offline (MainMenu opened on its own): the "other player" is faked with the two checkboxes below.
// Tick one (even during Play mode) and that role greys out on the role select screen.
//
// RoleSelector only talks to IRoleClaimService, so it works the same either way.

using System;
using UnityEngine;

public class LocalRoleClaimService : MonoBehaviour, IRoleClaimService
{
    [Header("Offline only: pretend the other player took this role")]
    [SerializeField] private bool debugGrannyTaken;
    [SerializeField] private bool debugPrisonerTaken;

    [Header("Read only: this player's claim")]
    [SerializeField] private PlayerRole myClaim = PlayerRole.None;

    public event Action ClaimsChanged;

    // The networked claims, if this menu was reached from the Lobby.
    private static NetworkRoleSelect Online =>
        NetworkRoleSelect.Instance != null && NetworkRoleSelect.Instance.IsReady ? NetworkRoleSelect.Instance : null;

    private void OnEnable() => NetworkRoleSelect.Changed += OnNetworkChanged;
    private void OnDisable() => NetworkRoleSelect.Changed -= OnNetworkChanged;
    private void OnNetworkChanged() => ClaimsChanged?.Invoke();

    public bool IsRoleTaken(PlayerRole role)
    {
        if (Online != null) return Online.IsTakenByOther(role);

        switch (role)
        {
            case PlayerRole.Granny: return debugGrannyTaken;
            case PlayerRole.Prisoner: return debugPrisonerTaken;
            default: return false;
        }
    }

    public bool TryClaim(PlayerRole role)
    {
        if (role == PlayerRole.None || IsRoleTaken(role)) return false;

        // Online the master has the final say; if both tap the same role at the same moment, only one
        // gets it and the other's READY is ignored.
        if (Online != null) Online.RequestClaim(role);

        myClaim = role;
        Debug.Log($"[LocalRoleClaimService] Claimed {role}");
        ClaimsChanged?.Invoke();
        return true;
    }

    public void ReleaseClaim()
    {
        if (myClaim == PlayerRole.None) return;

        if (Online != null) Online.RequestClaim(PlayerRole.None);
        Debug.Log($"[LocalRoleClaimService] Released {myClaim}");
        myClaim = PlayerRole.None;
        ClaimsChanged?.Invoke();
    }

    // Called when a checkbox is changed in the Inspector, so ticking one during Play updates the menu live.
    private void OnValidate()
    {
        if (Application.isPlaying) ClaimsChanged?.Invoke();
    }
}
