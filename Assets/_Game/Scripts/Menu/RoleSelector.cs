// RoleSelector.cs
//
// Role select panel: the player taps the Granny or Prisoner portrait (it gets highlighted),
// then presses START. START stays greyed out until a role is picked. A role another player has
// already claimed is greyed out and can't be picked. On START the role is claimed, saved in
// GameSession, and the menu moves on to the weapon loadout (Granny) or the briefing (Prisoner).
//
// Setup: put this on RoleSelectPanel. Hook up in the Inspector:
//   Granny portrait Button OnClick   -> RoleSelector.SelectGranny
//   Prisoner portrait Button OnClick -> RoleSelector.SelectPrisoner
//   START Button OnClick             -> RoleSelector.OnStartPressed
// The highlight objects are shown/hidden to mark the picked portrait (e.g. a coloured frame).
// Role Claim Service: drag in the object with LocalRoleClaimService (or a networked one later).
// If it's left empty, no role is ever taken.

using UnityEngine;
using UnityEngine.UI;

public class RoleSelector : MonoBehaviour
{
    [SerializeField] private MenuController menuController;
    [SerializeField] private Button grannyButton;
    [SerializeField] private Button prisonerButton;
    [SerializeField] private Button startButton;
    [SerializeField] private GameObject grannyHighlight;
    [SerializeField] private GameObject prisonerHighlight;

    [Tooltip("Any component that implements IRoleClaimService, e.g. LocalRoleClaimService.")]
    [SerializeField] private MonoBehaviour roleClaimService;

    private PlayerRole pendingRole = PlayerRole.None;

    private IRoleClaimService Claims => roleClaimService as IRoleClaimService;

    // Runs every time the panel is shown, so coming back here starts fresh.
    private void OnEnable()
    {
        pendingRole = PlayerRole.None;
        if (Claims != null)
        {
            Claims.ReleaseClaim();
            Claims.ClaimsChanged += Refresh;
        }
        Refresh();
    }

    private void OnDisable()
    {
        if (Claims != null) Claims.ClaimsChanged -= Refresh;
    }

    public void SelectGranny() => Select(PlayerRole.Granny);
    public void SelectPrisoner() => Select(PlayerRole.Prisoner);

    public void OnStartPressed()
    {
        if (pendingRole == PlayerRole.None) return;

        if (GameSession.Instance == null)
        {
            Debug.LogError("[RoleSelector] No GameSession in the scene.", this);
            return;
        }

        if (Claims != null && !Claims.TryClaim(pendingRole))
        {
            Debug.Log($"[RoleSelector] {pendingRole} was already taken by the other player.", this);
            pendingRole = PlayerRole.None;
            Refresh();
            return;
        }
        GameSession.Instance.SelectedRole = pendingRole;

        if (pendingRole == PlayerRole.Granny)
            menuController.ShowGrannyLoadout();
        else
            menuController.ShowPrisonerBrief();
    }

    private void Select(PlayerRole role)
    {
        if (IsTaken(role)) return;
        pendingRole = role;
        Refresh();
    }

    private bool IsTaken(PlayerRole role) => Claims != null && Claims.IsRoleTaken(role);

    private void Refresh()
    {
        // If the other player just grabbed the role we had picked, drop our pick.
        if (IsTaken(pendingRole)) pendingRole = PlayerRole.None;

        // A non-interactable Button shows its Disabled Color, which greys the portrait out.
        if (grannyButton != null) grannyButton.interactable = !IsTaken(PlayerRole.Granny);
        if (prisonerButton != null) prisonerButton.interactable = !IsTaken(PlayerRole.Prisoner);

        if (grannyHighlight != null) grannyHighlight.SetActive(pendingRole == PlayerRole.Granny);
        if (prisonerHighlight != null) prisonerHighlight.SetActive(pendingRole == PlayerRole.Prisoner);
        if (startButton != null) startButton.interactable = pendingRole != PlayerRole.None;
    }

    // Warn in the Inspector if something that isn't a claim service is dragged in.
    private void OnValidate()
    {
        if (roleClaimService != null && Claims == null)
        {
            Debug.LogWarning($"[RoleSelector] {roleClaimService.GetType().Name} doesn't implement IRoleClaimService.", this);
            roleClaimService = null;
        }
    }
}
