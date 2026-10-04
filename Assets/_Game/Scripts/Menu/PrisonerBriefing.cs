// PrisonerBriefing.cs
//
// Fills the prisoner's briefing text (goal, time limit, stealth tips) and starts the game on READY.
// {time} in the briefing text is replaced with the round length, e.g. "5:00".
//
// Setup: put this on PrisonerBriefPanel, drag the body Text into Briefing Text, and hook up
//   READY Button OnClick -> PrisonerBriefing.OnReadyPressed

using UnityEngine;
using UnityEngine.UI;

public class PrisonerBriefing : MonoBehaviour
{
    [SerializeField] private MenuController menuController;
    [SerializeField] private Text briefingText;

    [Tooltip("Round length in seconds. Keep in sync with the round timer (300 s = 5 minutes).")]
    [SerializeField] private float roundSeconds = 300f;

    [TextArea(4, 10)]
    [SerializeField] private string briefing =
        "Find the key and use it to unlock the front door. You have {time}.\n" +
        "Opening doors and drawers makes noise. So does dropping the key.\n" +
        "Some floorboards creak. Granny is listening.\n" +
        "Hide under beds and in wardrobes when she's close.";

    private void OnEnable()
    {
        if (briefingText == null) return;
        int seconds = Mathf.RoundToInt(roundSeconds);
        briefingText.text = briefing.Replace("{time}", $"{seconds / 60}:{seconds % 60:00}");
    }

    public void OnReadyPressed()
    {
        menuController.StartGame();
    }
}
