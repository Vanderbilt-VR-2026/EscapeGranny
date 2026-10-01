// LocalRoleClaimService.cs
//
// Single-headset stand-in for role claiming until networking is chosen.
// The "other player" is faked with the two checkboxes below: tick one (even during Play mode)
// and that role greys out on the role select screen.
//
// Replace with a networked IRoleClaimService later; RoleSelector doesn't need to change.

using System;
using UnityEngine;

public class LocalRoleClaimService : MonoBehaviour, IRoleClaimService
{
    [Header("Pretend the other player took this role")]
    [SerializeField] private bool debugGrannyTaken;
    [SerializeField] private bool debugPrisonerTaken;

    [Header("Read only: this player's claim")]
    [SerializeField] private PlayerRole myClaim = PlayerRole.None;

    public event Action ClaimsChanged;

    public bool IsRoleTaken(PlayerRole role)
    {
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

        myClaim = role;
        Debug.Log($"[LocalRoleClaimService] Claimed {role}");
        ClaimsChanged?.Invoke();
        return true;
    }

    public void ReleaseClaim()
    {
        if (myClaim == PlayerRole.None) return;

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
