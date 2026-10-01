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
    public Color charcoal = Hex("1C2120");   // panel background, fog
    public Color navy = Hex("0C1B31");       // Prisoner
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
    public int bodySize = 28;

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
