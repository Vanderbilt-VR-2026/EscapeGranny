// IRoleClaimService.cs
//
// Makes sure the two players end up on opposite sides: once one player claims a role,
// it greys out for the other. The menu only talks to this interface, so the local stub
// (LocalRoleClaimService) can later be swapped for a networked version without touching the menu.

using System;

public interface IRoleClaimService
{
    // Fired whenever any claim changes, so the menu can refresh which portraits are greyed out.
    event Action ClaimsChanged;

    // True if ANOTHER player already holds this role.
    bool IsRoleTaken(PlayerRole role);

    // Claims the role for this player. Returns false if another player got there first.
    bool TryClaim(PlayerRole role);

    // Gives up this player's claim (e.g. they went back to role select).
    void ReleaseClaim();
}
