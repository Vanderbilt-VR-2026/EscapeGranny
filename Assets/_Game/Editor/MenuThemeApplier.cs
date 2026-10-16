// MenuThemeApplier.cs
//
// Restyles the open main menu scene with the moodboard look from MenuTheme: Big Shoulders fonts,
// the five moodboard colours, a see-through dark panel, and no blue default sky. (The room's own
// lighting comes from HouseBuilder's horror lighting, which this leaves alone.)
//
// How to use:
//   Runs automatically as part of Tools > Escape Granny > Build Main Menu (MainMenuSceneBuilder).
//   The first build creates Assets/_Game/Settings/MenuTheme.asset. Tweak colours, fonts or sizes
//   there and rebuild the menu.
//
// It finds things by the names MainMenuSceneBuilder gives them (Title, Heading, Label,
// PlayButton, GrannyPortrait, AxeOption, Highlight, ...). If you rename objects, update the
// matching rules in StyleText / StyleButton below.

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MenuThemeApplier
{
    const string ThemePath = "Assets/_Game/Settings/MenuTheme.asset";
    const string DisplayFontPath = "Assets/_Game/Fonts/BigShoulders-Black.ttf";
    const string BodyFontPath = "Assets/_Game/Fonts/BigShoulders-SemiBold.ttf";

    // Styles everything under the menu canvas plus the room around it. Returns false (and changes
    // nothing) if the theme is missing a font. The caller saves the scene.
    public static bool Apply(GameObject canvas)
    {
        var theme = LoadOrCreateTheme();
        if (theme.displayFont == null || theme.bodyFont == null)
        {
            Debug.LogWarning("[MenuThemeApplier] MenuTheme is missing a font. Assign Display Font and Body Font on " +
                             ThemePath + ", then rebuild the menu.", theme);
            return false;
        }

        foreach (var image in canvas.GetComponentsInChildren<Image>(true))
            StyleImage(image, theme);
        foreach (var button in canvas.GetComponentsInChildren<Button>(true))
            StyleButton(button, theme);
        foreach (var text in canvas.GetComponentsInChildren<Text>(true))
            StyleText(text, theme);

        StyleEnvironment(theme);
        return true;
    }

    // Decorative images, by the names MainMenuSceneBuilder gives them.
    static void StyleImage(Image image, MenuTheme theme)
    {
        switch (image.name)
        {
            case "Background": SetColor(image, WithAlpha(theme.voidBlack, theme.panelOpacity)); break; // the room shows through
            case "Highlight": SetColor(image, theme.mustard); break;    // frame around the picked card
            case "Glow": SetColor(image, WithAlpha(theme.bloodRed, 0.3f)); break;
            case "Corner": SetColor(image, WithAlpha(theme.ash, 0.45f)); break;
            case "Divider": SetColor(image, theme.bloodRed); break;
            case "BriefCard": SetColor(image, theme.charcoal); break;
            case "StageGlow": // the role's colour glowing behind its 3D character
                SetColor(image, WithAlpha(RoleColor(image.transform, theme), 0.5f));
                break;
            case "Accent": // colour stripe on a card: the role's colour, red otherwise
                SetColor(image, RoleColor(image.transform, theme));
                break;
        }
    }

    static void StyleButton(Button button, MenuTheme theme)
    {
        Color color;
        if (button.name.EndsWith("Portrait")) color = theme.charcoal; // role cards: dark, coloured by stripe + name
        else if (button.name == "BackButton") color = theme.charcoal;
        else if (IsWeaponOption(button.transform)) color = theme.darkBrown;
        else color = theme.bloodRed; // PLAY, START, CONFIRM, READY

        if (button.targetGraphic != null) SetColor(button.targetGraphic, color);

        // Tints multiply the colour above: slightly dim at rest, full colour when a ray hovers,
        // darker on press, grey when taken/disabled.
        Undo.RecordObject(button, "Style button");
        var colors = button.colors;
        colors.normalColor = new Color(0.82f, 0.82f, 0.82f);
        colors.highlightedColor = Color.white;
        colors.selectedColor = new Color(0.82f, 0.82f, 0.82f);
        colors.pressedColor = new Color(0.6f, 0.6f, 0.6f);
        colors.disabledColor = theme.takenGrey;
        colors.colorMultiplier = 1f;
        button.colors = colors;
    }

    static void StyleText(Text text, MenuTheme theme)
    {
        Undo.RecordObject(text, "Style text");
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow; // tall condensed caps would get cut off otherwise

        var button = text.GetComponentInParent<Button>(true);
        switch (text.name)
        {
            case "Title":
                text.font = theme.displayFont;
                text.fontSize = theme.titleSize;
                text.color = theme.bloodRed;
                text.text = text.text.ToUpperInvariant();
                // White offset copy behind the letters, like the moodboard title.
                SetShadow(text.gameObject, theme.bone, theme.titleShadowOffset);
                break;

            case "Heading" when text.transform.parent.name == "HomePanel":
                text.font = theme.bodyFont;
                text.fontSize = theme.bodySize + 4;
                text.color = theme.ash;
                text.text = theme.homeTagline.ToUpperInvariant();
                break;

            case "Heading":
                text.font = theme.displayFont;
                text.fontSize = theme.headingSize;
                text.color = theme.ash;
                break;

            case "Label" when button != null && button.name == "BackButton":
                text.font = theme.displayFont;
                text.fontSize = theme.backLabelSize;
                text.color = theme.ash;
                break;

            case "Label" when button != null && button.name.EndsWith("Portrait"):
                text.font = theme.displayFont;
                text.fontSize = theme.roleNameSize;
                text.color = RoleColor(text.transform, theme);
                SetShadow(text.gameObject, WithAlpha(Color.black, 0.6f), new Vector2(2, -2));
                break;

            case "RoleName": // big name beside the turning character
                text.font = theme.displayFont;
                text.fontSize = theme.roleNameSize;
                text.color = RoleColor(text.transform, theme);
                SetShadow(text.gameObject, WithAlpha(Color.black, 0.6f), new Vector2(2, -2));
                break;

            case "PickHint":
                text.font = theme.displayFont;
                text.fontSize = theme.headingSize;
                text.color = WithAlpha(theme.ash, 0.6f);
                break;

            case "Label" when button != null:
                text.font = theme.displayFont;
                text.fontSize = IsWeaponOption(button.transform) ? theme.optionLabelSize : theme.actionLabelSize;
                text.color = theme.bone;
                SetShadow(text.gameObject, WithAlpha(Color.black, 0.6f), new Vector2(2, -2));
                break;

            case "Step":
            case "Tag":
                text.font = theme.displayFont;
                text.fontSize = theme.smallSize;
                text.color = theme.mustard;
                break;

            case "Subtitle":
            case "Subtext":
                text.font = theme.bodyFont;
                text.fontSize = theme.smallSize;
                text.color = theme.ash;
                break;

            case "Hint":
                text.font = theme.bodyFont;
                text.fontSize = theme.smallSize;
                text.color = WithAlpha(theme.ash, 0.7f);
                break;

            case "Stats":
                text.font = theme.bodyFont;
                text.fontSize = theme.smallSize;
                text.color = theme.bone;
                break;

            case "TakenLabel": // rubber stamp over a role the other player claimed
                text.font = theme.displayFont;
                text.fontSize = theme.roleNameSize;
                text.color = theme.bone;
                SetShadow(text.gameObject, WithAlpha(Color.black, 0.8f), new Vector2(3, -3));
                break;

            case "Description":
                text.font = theme.bodyFont;
                text.fontSize = theme.bodySize;
                text.color = theme.mustard;
                break;

            default: // BriefingText and anything added later
                text.font = theme.bodyFont;
                text.fontSize = theme.bodySize;
                text.color = theme.bone;
                break;
        }
    }

    // Dark sky outside the windows and doorways: no blue default skybox. Fog, ambient light and the
    // moonlight come from HouseBuilder's horror lighting (applied before this), so they're left alone.
    static void StyleEnvironment(MenuTheme theme)
    {
        var sceneCamera = Object.FindAnyObjectByType<Camera>();
        if (sceneCamera != null)
        {
            Undo.RecordObject(sceneCamera, "Style camera");
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = RenderSettings.fog ? RenderSettings.fogColor : theme.voidBlack; // matches the fog
        }

        // RenderSettings can't be undone with Cmd/Ctrl+Z; rebuild the menu instead.
        RenderSettings.skybox = null;
    }

    // Prisoner things (anything under PrisonerPortrait or PrisonerPreview) are blue, the rest Granny red.
    static Color RoleColor(Transform t, MenuTheme theme)
    {
        for (; t != null; t = t.parent)
            if (t.name.StartsWith("Prisoner")) return theme.coldBlue;
        return theme.bloodRed;
    }

    static bool IsWeaponOption(Transform buttonTransform)
    {
        var parent = buttonTransform.parent;
        return parent != null && parent.name.EndsWith("Option") && !buttonTransform.name.EndsWith("Portrait");
    }

    static void SetColor(Graphic graphic, Color color)
    {
        Undo.RecordObject(graphic, "Style colour");
        graphic.color = color;
    }

    // Uses Shadow exactly (Outline is a subclass of Shadow, so GetComponent<Shadow> could return one).
    static void SetShadow(GameObject go, Color color, Vector2 distance)
    {
        Shadow shadow = null;
        foreach (var s in go.GetComponents<Shadow>())
            if (s.GetType() == typeof(Shadow)) shadow = s;
        if (shadow == null) shadow = Undo.AddComponent<Shadow>(go);
        else Undo.RecordObject(shadow, "Style shadow");

        shadow.effectColor = color;
        shadow.effectDistance = distance;
    }

    static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    static MenuTheme LoadOrCreateTheme()
    {
        var theme = AssetDatabase.LoadAssetAtPath<MenuTheme>(ThemePath);
        if (theme != null) return theme;

        EnsureFolder("Assets/_Game/Settings");
        theme = ScriptableObject.CreateInstance<MenuTheme>();
        theme.displayFont = AssetDatabase.LoadAssetAtPath<Font>(DisplayFontPath);
        theme.bodyFont = AssetDatabase.LoadAssetAtPath<Font>(BodyFontPath);
        AssetDatabase.CreateAsset(theme, ThemePath);
        AssetDatabase.SaveAssets();
        Debug.Log("[MenuThemeApplier] Created " + ThemePath + ". Tweak it there and rerun Apply Menu Theme.");
        return theme;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
