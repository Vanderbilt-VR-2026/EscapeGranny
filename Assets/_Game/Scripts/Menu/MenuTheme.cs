// MenuTheme.cs
//
// The menu's look in one place: fonts and colours taken from the team moodboard.
// Lives as an asset at Assets/_Game/Settings/MenuTheme.asset. Change values there, then run
// Tools > Escape Granny > Apply Menu Theme to restyle the open menu scene.

using UnityEngine;

[CreateAssetMenu(fileName = "MenuTheme", menuName = "Escape Granny/Menu Theme")]
public class MenuTheme : ScriptableObject
{
    [Header("Fonts (Big Shoulders, same family as the proposal deck)")]
    public Font displayFont;   // title, headings, button labels
    public Font bodyFont;      // briefing and description text

    [Header("Moodboard palette")]
    public Color bloodRed = Hex("A62F23");   // title, action buttons, Granny
    public Color darkBrown = Hex("31220C");  // wood: weapon buttons
    public Color charcoal = Hex("1C2120");   // dark accents
    public Color voidBlack = Hex("000000");  // camera background and menu panel
    [Range(0f, 1f)] public float panelOpacity = 0.82f; // menu panel: see-through enough to show the room behind
    public Color navy = Hex("0C1B31");       // Prisoner (dark)
    public Color coldBlue = Hex("4A78B0");   // Prisoner accents: a brighter navy that shows up on black
    public Color mustard = Hex("967800");    // the key: selection frames, descriptions

    [Header("Neutrals")]
    public Color bone = Hex("E8E1D3");       // button labels, body text, title shadow
    public Color ash = Hex("A59E90");        // headings (the grey-beige "MOODBOARD" text)
    public Color takenGrey = new Color(0.25f, 0.25f, 0.25f, 0.6f);

    [Header("Sizes")]
    public int titleSize = 88;
    public int headingSize = 40;
    public int actionLabelSize = 52;
    public int optionLabelSize = 38;
    public int roleNameSize = 60;  // GRANNY / PRISONER on the role cards
    public int bodySize = 28;
    public int smallSize = 22;     // step labels, role card stats, weapon tags, controls hint
    public int backLabelSize = 32;

    [Tooltip("How far the off-white copy behind the title is shifted (right, down). Smaller = layers closer together.")]
    public Vector2 titleShadowOffset = new Vector2(3, -3);

    [Header("Text")]
    public string homeTagline = "Two players. One key. Five minutes.";

    // Plain C# (no Unity API), because field initializers run while Unity is constructing the asset.
    private static Color Hex(string hex)
    {
        int rgb = System.Convert.ToInt32(hex, 16);
        return new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    }
}
