// MenuController.cs
//
// Shows one menu panel at a time. Buttons call the Show...() methods from their OnClick list.
//
// Setup: put this on an object in the menu scene and drag the four panel GameObjects
// (children of the menu canvas) into the fields below.

using UnityEngine;

public class MenuController : MonoBehaviour
{
    [SerializeField] private GameObject homePanel;
    [SerializeField] private GameObject roleSelectPanel;
    [SerializeField] private GameObject grannyLoadoutPanel;
    [SerializeField] private GameObject prisonerBriefPanel;

    private void Start()
    {
        ShowHome();
    }

    public void ShowHome() => ShowOnly(homePanel);
    public void ShowRoleSelect() => ShowOnly(roleSelectPanel);
    public void ShowGrannyLoadout() => ShowOnly(grannyLoadoutPanel);
    public void ShowPrisonerBrief() => ShowOnly(prisonerBriefPanel);

    // Called when the player is ready (Granny confirmed a weapon, or the prisoner pressed READY).
    // Step 9 makes this load the house scene; for now it only logs.
    public void StartGame()
    {
        var session = GameSession.Instance;
        Debug.Log(session != null
            ? $"[MenuController] StartGame: Role = {session.SelectedRole}, Weapon = {session.SelectedWeapon}"
            : "[MenuController] StartGame: no GameSession in the scene.");
    }

    private void ShowOnly(GameObject panel)
    {
        if (panel == null)
        {
            Debug.LogWarning("[MenuController] Panel not assigned in the Inspector.", this);
            return;
        }

        SetActive(homePanel, panel == homePanel);
        SetActive(roleSelectPanel, panel == roleSelectPanel);
        SetActive(grannyLoadoutPanel, panel == grannyLoadoutPanel);
        SetActive(prisonerBriefPanel, panel == prisonerBriefPanel);
        Debug.Log($"[MenuController] Showing {panel.name}");
    }

    // Unity objects need == null, not ?. (which skips Unity's null check).
    private static void SetActive(GameObject panel, bool active)
    {
        if (panel != null) panel.SetActive(active);
    }
}
