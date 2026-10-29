// GrannyWeaponTutorial.cs
//
// Granny's "how to use your weapon" screen, shown after she confirms a weapon on the loadout screen
// and before she's dropped into the house. Works like PrisonerBriefing: fills in the text for the
// weapon she picked, READY starts the game, BACK returns to the weapon picker.
// {fuse} in the bomb text is replaced with the fuse time, e.g. "2.5".
//
// Setup: run Tools > Escape Granny > Build Granny Weapon Tutorial Panel in MainMenu.
// (Or by hand: put this on GrannyTutorialPanel, drag in the Heading and body Text, and hook up
//   READY Button OnClick -> GrannyWeaponTutorial.OnReadyPressed
//   BACK  Button OnClick -> GrannyWeaponTutorial.OnBackPressed
// then drag the panel into MenuController's Granny Tutorial Panel field.)

using UnityEngine;
using UnityEngine.UI;

public class GrannyWeaponTutorial : MonoBehaviour
{
    [SerializeField] private MenuController menuController;
    [SerializeField] private Text headingText;
    [SerializeField] private Text bodyText;

    [Tooltip("Keep in sync with Fuse Time on the Bomb script on Bomb_Root (2.5 s by default).")]
    [SerializeField] private float bombFuseSeconds = 2.5f;

    [Header("Text (shown for every weapon, after the weapon's own text)")]
    [TextArea(2, 6)]
    [SerializeField] private string gripRule =
        "Keep holding the side GRIP button the whole time. If you let go you'll drop your weapon, " +
        "and the noise will reveal your position.";

    [Header("Text for each weapon")]
    [TextArea(2, 6)]
    [SerializeField] private string axeText =
        "Swing the axe to strike. Anything it hits gets scratched.\n" +
        "Get within arm's reach of the prisoner and swing.";

    [TextArea(2, 6)]
    [SerializeField] private string knifeText =
        "Swing or thrust the knife to strike. Anything it hits gets scratched.\n" +
        "Get within arm's reach of the prisoner and thrust.";

    [TextArea(2, 6)]
    [SerializeField] private string gunText =
        "Aim, then press the front TRIGGER button to shoot.\n" +
        "Ammo is limited, so make every bullet count.";

    [TextArea(2, 6)]
    [SerializeField] private string bombText =
        "Pull the pin out with your other hand to arm the bomb.\n" +
        "Then throw it. The fuse starts when it leaves your hand, and it explodes {fuse} seconds later.";

    private void OnEnable()
    {
        var weapon = GameSession.Instance != null ? GameSession.Instance.SelectedWeapon : WeaponType.None;

        string name, howTo;
        switch (weapon)
        {
            case WeaponType.Axe:   name = "AXE";   howTo = axeText; break;
            case WeaponType.Knife: name = "KNIFE"; howTo = knifeText; break;
            case WeaponType.Gun:   name = "GUN";   howTo = gunText; break;
            case WeaponType.Bomb:  name = "BOMB";  howTo = bombText.Replace("{fuse}", bombFuseSeconds.ToString("0.#")); break;
            default:               name = "WEAPON"; howTo = ""; break;
        }

        if (headingText != null) headingText.text = $"HOW TO USE THE {name}";
        if (bodyText != null) bodyText.text = string.IsNullOrEmpty(howTo) ? gripRule : howTo + "\n\n" + gripRule;
    }

    public void OnReadyPressed()
    {
        menuController.StartGame();
    }

    public void OnBackPressed()
    {
        menuController.ShowGrannyLoadout();
    }
}
