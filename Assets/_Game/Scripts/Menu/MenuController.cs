// MenuController.cs
//
// Shows one menu panel at a time. Buttons call the Show...() methods from their OnClick list.
// The new panel fades in and settles from slightly smaller, instead of popping in.
//
// Setup: put this on an object in the menu scene and drag the panel GameObjects
// (children of the menu canvas) into the fields below. Granny Tutorial Panel is optional:
// if it's empty, confirming a weapon starts the game straight away.

using System.Collections;
using UnityEngine;

public class MenuController : MonoBehaviour
{
    [SerializeField] private GameObject homePanel;
    [SerializeField] private GameObject roleSelectPanel;
    [SerializeField] private GameObject grannyLoadoutPanel;
    [SerializeField] private GameObject prisonerBriefPanel;
    [Tooltip("Optional. Shown after Granny confirms a weapon, before the game starts.")]
    [SerializeField] private GameObject grannyTutorialPanel;
    [SerializeField, Min(0f)] private float fadeSeconds = 0.3f;
    [Tooltip("Scene loaded after the role screens. It must be in the build's scene list.")]
    [SerializeField] private string gameSceneName = "House_Bishal";

    private Coroutine fade;

    private void Start()
    {
        ShowHome();
    }

    public void ShowHome() => ShowOnly(homePanel);
    public void ShowRoleSelect() => ShowOnly(roleSelectPanel);
    public void ShowGrannyLoadout() => ShowOnly(grannyLoadoutPanel);
    public void ShowPrisonerBrief() => ShowOnly(prisonerBriefPanel);
    public void ShowGrannyTutorial() => ShowOnly(grannyTutorialPanel);

    public bool HasGrannyTutorial => grannyTutorialPanel != null;

    // Called when the player is ready (Granny pressed READY on her weapon tutorial, or the prisoner pressed READY).
    // For now this loads the house offline (one headset, no Photon) for testing; the networked
    // version will go through the Lobby instead.
    public void StartGame()
    {
        var session = GameSession.Instance;
        if (session == null)
        {
            Debug.LogError("[MenuController] StartGame: no GameSession in the scene.", this);
            return;
        }
        Debug.Log($"[MenuController] StartGame: Role = {session.SelectedRole}, Weapon = {session.SelectedWeapon}");
        OfflineGameLauncher.Launch(gameSceneName, session.SelectedRole);
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
        SetActive(grannyTutorialPanel, panel == grannyTutorialPanel);
        Debug.Log($"[MenuController] Showing {panel.name}");

        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(FadeIn(panel));
    }

    private IEnumerator FadeIn(GameObject panel)
    {
        var group = panel.GetComponent<CanvasGroup>();
        if (group == null) group = panel.AddComponent<CanvasGroup>();

        for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
        {
            float eased = 1f - Mathf.Pow(1f - t / fadeSeconds, 3f); // fast start, gentle stop
            group.alpha = eased;
            panel.transform.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, eased);
            yield return null;
        }
        group.alpha = 1f;
        panel.transform.localScale = Vector3.one;
        fade = null;
    }

    // Unity objects need == null, not ?. (which skips Unity's null check).
    private static void SetActive(GameObject panel, bool active)
    {
        if (panel != null) panel.SetActive(active);
    }
}
