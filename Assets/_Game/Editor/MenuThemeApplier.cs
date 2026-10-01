// MenuThemeApplier.cs
//
// Restyles the open main menu scene with the moodboard look from MenuTheme: Big Shoulders fonts,
// the five moodboard colours, a dark foggy room instead of the blue default sky.
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
    const string FloorMaterialPath = "Assets/_Game/Materials/MenuFloor.mat";

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
        {
            if (image.name == "Background") SetColor(image, WithAlpha(theme.charcoal, 0.94f));
            else if (image.name == "Highlight") SetColor(image, theme.mustard);
        }
        foreach (var button in canvas.GetComponentsInChildren<Button>(true))
            StyleButton(button, theme);
        foreach (var text in canvas.GetComponentsInChildren<Text>(true))
            StyleText(text, theme);

        StyleEnvironment(theme);
        return true;
    }

    static void StyleButton(Button button, MenuTheme theme)
    {
        Color color;
        if (button.name == "GrannyPortrait") color = theme.bloodRed;
        else if (button.name == "PrisonerPortrait") color = theme.navy;
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

            case "Label" when button != null:
                bool isOption = button.name.EndsWith("Portrait") || IsWeaponOption(button.transform);
                text.font = theme.displayFont;
                text.fontSize = isOption ? theme.optionLabelSize : theme.actionLabelSize;
                text.color = theme.bone;
                SetShadow(text.gameObject, WithAlpha(Color.black, 0.6f), new Vector2(2, -2));
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

    // Dark, foggy menu room: no blue sky, dim warm light, dark wooden floor.
    static void StyleEnvironment(MenuTheme theme)
    {
        var sceneCamera = Object.FindAnyObjectByType<Camera>();
        if (sceneCamera != null)
        {
            Undo.RecordObject(sceneCamera, "Style camera");
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = theme.charcoal * 0.5f;
        }

        // RenderSettings can't be undone with Cmd/Ctrl+Z; rerun with other theme values instead.
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.14f, 0.12f, 0.11f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.12f;
        RenderSettings.fogColor = theme.charcoal * 0.5f;

        var sun = GameObject.Find("Directional Light");
        if (sun != null && sun.TryGetComponent(out Light light))
        {
            Undo.RecordObject(light, "Style light");
            light.intensity = 0.4f;
            light.color = new Color(1f, 0.82f, 0.65f);
        }

        var floor = GameObject.Find("Floor");
        if (floor != null && floor.TryGetComponent(out Renderer floorRenderer))
        {
            Undo.RecordObject(floorRenderer, "Style floor");
            floorRenderer.sharedMaterial = LoadOrCreateFloorMaterial(theme.darkBrown);
        }
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

    static Material LoadOrCreateFloorMaterial(Color color)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterialPath);
        if (material == null)
        {
            EnsureFolder("Assets/_Game/Materials");
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, FloorMaterialPath);
        }
        Undo.RecordObject(material, "Style floor material");
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", 0.1f);
        EditorUtility.SetDirty(material);
        return material;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
