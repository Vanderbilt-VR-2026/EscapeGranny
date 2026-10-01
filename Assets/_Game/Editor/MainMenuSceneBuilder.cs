// MainMenuSceneBuilder.cs
//
// Builds the whole main menu scene in one click, so nobody has to set up the XR rig, UI canvas,
// panels and button wiring by hand.
//
// How to use:
//   Menu: Tools > Escape Granny > Build Main Menu.
//   It (re)creates Assets/_Game/Scenes/MainMenu.unity containing:
//     - XR Origin (XR Rig) from the XRI Starter Assets sample, at (0, 0, 0)
//     - a floor, the directional light, and an EventSystem with XR UI Input Module
//     - GameSession (remembers role + weapon), RoleClaimService (fake "other player" checkboxes)
//       and MenuController (shows one panel at a time)
//     - MenuCanvas: world-space canvas 2 m in front of the player with four panels:
//         HomePanel          tagline + PLAY
//         RoleSelectPanel    Granny / Prisoner portraits + START       (RoleSelector)
//         GrannyLoadoutPanel Axe / Knife / Gun / Bomb + CONFIRM         (WeaponLoadout)
//         PrisonerBriefPanel briefing text + READY                     (PrisonerBriefing)
//     - XR Device Simulator, if its sample has been imported (keyboard and mouse fake VR for Macs)
//   Then it applies the moodboard look (MenuThemeApplier) and puts the 3D weapon models on the
//   loadout buttons (WeaponModelBuilder).
//
//   Running it again asks before overwriting the scene, and anything changed by hand in the scene
//   is lost. To tweak the look, edit Assets/_Game/Settings/MenuTheme.asset and rebuild.

using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

public static class MainMenuSceneBuilder
{
    const string ScenePath = "Assets/_Game/Scenes/MainMenu.unity";
    const string RigPrefabPath =
        "Assets/Samples/XR Interaction Toolkit/3.3.2/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

    // Newer XRI simulator first, then the classic one.
    static readonly string[] SimulatorPrefabNames = { "XR Interaction Simulator", "XR Device Simulator" };

    // Canvas is 800 x 500 UI units; at 0.002 m per unit that is 1.6 m x 1 m in the world.
    static readonly Vector2 CanvasSize = new Vector2(800, 500);
    const float CanvasScale = 0.002f;
    static readonly Vector3 CanvasPosition = new Vector3(0f, 1.5f, 2f);

    static readonly (WeaponType weapon, string description)[] Weapons =
    {
        (WeaponType.Axe, "Near. Get within arm's reach and swing to kill."),
        (WeaponType.Knife, "Near. Get within arm's reach and thrust to kill."),
        (WeaponType.Gun, "Far. Shoot from a distance, but ammo is limited."),
        (WeaponType.Bomb, "Far. Throw it at a target."),
    };

    [MenuItem("Tools/Escape Granny/Build Main Menu")]
    public static void BuildMainMenu()
    {
        var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefabPath);
        if (rigPrefab == null)
        {
            EditorUtility.DisplayDialog("Main Menu Builder",
                "Couldn't find the XR Origin (XR Rig) prefab at:\n" + RigPrefabPath +
                "\n\nImport it via Window > Package Manager > XR Interaction Toolkit > Samples > Starter Assets.",
                "OK");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
            !EditorUtility.DisplayDialog("Main Menu Builder",
                ScenePath + " already exists. Rebuild it? Anything you changed in it by hand will be lost.",
                "Rebuild", "Cancel"))
            return;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EnsureFolder("Assets/_Game");
        EnsureFolder("Assets/_Game/Scenes");

        // Default scene = Main Camera + Directional Light. The rig brings its own camera.
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var defaultCamera = GameObject.Find("Main Camera");
        if (defaultCamera != null) Object.DestroyImmediate(defaultCamera);

        var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab, scene);
        rig.transform.position = Vector3.zero;

        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";

        new GameObject("EventSystem", typeof(EventSystem), typeof(XRUIInputModule));
        new GameObject("GameSession", typeof(GameSession));
        var claims = new GameObject("RoleClaimService").AddComponent<LocalRoleClaimService>();
        var controller = new GameObject("MenuController").AddComponent<MenuController>();

        var canvas = BuildCanvas(rig.GetComponentInChildren<Camera>());
        var home = BuildHomePanel(canvas.transform, controller);
        var roleSelect = BuildRoleSelectPanel(canvas.transform, controller, claims);
        var loadout = BuildWeaponLoadoutPanel(canvas.transform, controller);
        var brief = BuildPrisonerBriefPanel(canvas.transform, controller);

        var so = new SerializedObject(controller);
        so.FindProperty("homePanel").objectReferenceValue = home;
        so.FindProperty("roleSelectPanel").objectReferenceValue = roleSelect;
        so.FindProperty("grannyLoadoutPanel").objectReferenceValue = loadout.gameObject;
        so.FindProperty("prisonerBriefPanel").objectReferenceValue = brief;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Only Home visible while editing; MenuController does the same in Play mode.
        // To edit another panel, untick HomePanel and tick that one in the Hierarchy.
        roleSelect.SetActive(false);
        loadout.gameObject.SetActive(false);
        brief.SetActive(false);

        bool simulatorAdded = AddDeviceSimulator(scene);
        bool themed = MenuThemeApplier.Apply(canvas);
        int models = WeaponModelBuilder.AddModels(loadout);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        string msg = "Built " + ScenePath + ". Press Play to try it.";
        if (!themed)
            msg += "\n\nThe moodboard theme was skipped: MenuTheme is missing a font (see the Console).";
        if (models < Weapons.Length)
            msg += $"\n\nOnly {models} of {Weapons.Length} weapon models were added (see the Console).";
        if (!simulatorAdded)
            msg += "\n\nNo XR Device Simulator found. On a Mac, import it via Window > Package Manager > " +
                   "XR Interaction Toolkit > Samples > XR Device Simulator, then rebuild.";
        EditorUtility.DisplayDialog("Main Menu Builder", msg, "OK");
    }

    static GameObject BuildCanvas(Camera eventCamera)
    {
        var canvasGo = new GameObject("MenuCanvas", typeof(RectTransform));
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = eventCamera;
        canvasGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f; // keeps text crisp in world space
        canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();           // lets XR rays hit this canvas

        var rt = (RectTransform)canvasGo.transform;
        rt.sizeDelta = CanvasSize;
        rt.position = CanvasPosition;
        rt.localScale = Vector3.one * CanvasScale;

        var background = CreateUiObject("Background", canvasGo.transform);
        Stretch(background);
        background.gameObject.AddComponent<Image>();

        var title = CreateText("Title", canvasGo.transform, "Escape the Granny", 64);
        title.rectTransform.anchoredPosition = new Vector2(0, 140);
        title.rectTransform.sizeDelta = new Vector2(760, 100);
        return canvasGo;
    }

    static GameObject BuildHomePanel(Transform canvas, MenuController controller)
    {
        // The heading text is replaced by MenuTheme's tagline when the theme is applied.
        var panel = CreatePanel("HomePanel", canvas, "Two players. One key. Five minutes.",
            new Vector2(0, 40), new Vector2(760, 80));

        var play = CreateButton("PlayButton", panel.transform, "PLAY", new Vector2(0, -120), new Vector2(300, 100));
        UnityEventTools.AddPersistentListener(play.onClick, controller.ShowRoleSelect);
        return panel;
    }

    static GameObject BuildRoleSelectPanel(Transform canvas, MenuController controller, LocalRoleClaimService claims)
    {
        var panel = CreatePanel("RoleSelectPanel", canvas, "Choose your role");
        var selector = panel.AddComponent<RoleSelector>();

        var granny = CreateOption(panel.transform, "Granny", "GRANNY", new Vector2(-170, -50),
            new Vector2(160, 160), out var grannyHighlight);
        granny.name = "GrannyPortrait";
        var prisoner = CreateOption(panel.transform, "Prisoner", "PRISONER", new Vector2(170, -50),
            new Vector2(160, 160), out var prisonerHighlight);
        prisoner.name = "PrisonerPortrait";

        var start = CreateButton("StartButton", panel.transform, "START", new Vector2(0, -190), new Vector2(240, 70));

        UnityEventTools.AddPersistentListener(granny.onClick, selector.SelectGranny);
        UnityEventTools.AddPersistentListener(prisoner.onClick, selector.SelectPrisoner);
        UnityEventTools.AddPersistentListener(start.onClick, selector.OnStartPressed);

        var so = new SerializedObject(selector);
        so.FindProperty("menuController").objectReferenceValue = controller;
        so.FindProperty("grannyButton").objectReferenceValue = granny;
        so.FindProperty("prisonerButton").objectReferenceValue = prisoner;
        so.FindProperty("startButton").objectReferenceValue = start;
        so.FindProperty("grannyHighlight").objectReferenceValue = grannyHighlight;
        so.FindProperty("prisonerHighlight").objectReferenceValue = prisonerHighlight;
        so.FindProperty("roleClaimService").objectReferenceValue = claims;
        so.ApplyModifiedPropertiesWithoutUndo();
        return panel;
    }

    static WeaponLoadout BuildWeaponLoadoutPanel(Transform canvas, MenuController controller)
    {
        var panel = CreatePanel("GrannyLoadoutPanel", canvas, "Granny: choose a weapon");
        var loadout = panel.AddComponent<WeaponLoadout>();

        var so = new SerializedObject(loadout);
        var optionsProp = so.FindProperty("options");
        optionsProp.arraySize = Weapons.Length;

        for (int i = 0; i < Weapons.Length; i++)
        {
            var (weapon, description) = Weapons[i];
            var button = CreateOption(panel.transform, weapon.ToString(), weapon.ToString().ToUpper(),
                new Vector2(-270 + i * 180, -20), new Vector2(150, 110), out var highlight);
            UnityEventTools.AddIntPersistentListener(button.onClick, loadout.SelectWeapon, (int)weapon);

            var element = optionsProp.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("weapon").enumValueIndex = (int)weapon;
            element.FindPropertyRelative("button").objectReferenceValue = button;
            element.FindPropertyRelative("highlight").objectReferenceValue = highlight;
            element.FindPropertyRelative("description").stringValue = description;
        }

        var descriptionText = CreateText("Description", panel.transform, "", 24);
        descriptionText.rectTransform.anchoredPosition = new Vector2(0, -115);
        descriptionText.rectTransform.sizeDelta = new Vector2(760, 40);

        var confirm = CreateButton("ConfirmButton", panel.transform, "CONFIRM", new Vector2(0, -190), new Vector2(240, 70));
        UnityEventTools.AddPersistentListener(confirm.onClick, loadout.OnConfirmPressed);

        so.FindProperty("menuController").objectReferenceValue = controller;
        so.FindProperty("descriptionText").objectReferenceValue = descriptionText;
        so.FindProperty("confirmButton").objectReferenceValue = confirm;
        so.ApplyModifiedPropertiesWithoutUndo();
        return loadout;
    }

    static GameObject BuildPrisonerBriefPanel(Transform canvas, MenuController controller)
    {
        var panel = CreatePanel("PrisonerBriefPanel", canvas, "Prisoner: briefing");
        var briefing = panel.AddComponent<PrisonerBriefing>();

        // Filled in by PrisonerBriefing when the panel is shown.
        var body = CreateText("BriefingText", panel.transform, "(briefing text)", 24);
        body.rectTransform.anchoredPosition = new Vector2(0, -40);
        body.rectTransform.sizeDelta = new Vector2(720, 170);
        body.alignment = TextAnchor.UpperCenter;

        var ready = CreateButton("ReadyButton", panel.transform, "READY", new Vector2(0, -190), new Vector2(240, 70));
        UnityEventTools.AddPersistentListener(ready.onClick, briefing.OnReadyPressed);

        var so = new SerializedObject(briefing);
        so.FindProperty("menuController").objectReferenceValue = controller;
        so.FindProperty("briefingText").objectReferenceValue = body;
        so.ApplyModifiedPropertiesWithoutUndo();
        return panel;
    }

    // A full-size, transparent panel with a heading. The canvas title stays visible above it.
    static GameObject CreatePanel(string name, Transform canvas, string heading)
        => CreatePanel(name, canvas, heading.ToUpper(), new Vector2(0, 70), new Vector2(760, 50));

    static GameObject CreatePanel(string name, Transform canvas, string heading, Vector2 headingPosition, Vector2 headingSize)
    {
        var panel = CreateUiObject(name, canvas);
        Stretch(panel);

        var headingText = CreateText("Heading", panel, heading, 32);
        headingText.rectTransform.anchoredPosition = headingPosition;
        headingText.rectTransform.sizeDelta = headingSize;
        return panel.gameObject;
    }

    // A pickable option: a highlight frame (hidden until picked) peeking out 10 units around a
    // button. The option object is named <name>Option and the button <name>Button; MenuThemeApplier
    // colours them by those names.
    static Button CreateOption(Transform parent, string name, string label, Vector2 position, Vector2 size,
        out GameObject highlight)
    {
        var option = CreateUiObject(name + "Option", parent);
        option.anchoredPosition = position;
        option.sizeDelta = size + new Vector2(20, 20);

        // Created first so it draws behind the button, peeking out as a frame.
        var frame = CreateUiObject("Highlight", option);
        Stretch(frame);
        frame.gameObject.AddComponent<Image>().raycastTarget = false;
        frame.gameObject.SetActive(false);
        highlight = frame.gameObject;

        return CreateButton(name + "Button", option, label, Vector2.zero, size);
    }

    static Button CreateButton(string name, Transform parent, string label, Vector2 position, Vector2 size)
    {
        var rt = CreateUiObject(name, parent);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
        var image = rt.gameObject.AddComponent<Image>();
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        var text = CreateText("Label", rt, label, 40);
        Stretch(text.rectTransform);
        return button;
    }

    static bool AddDeviceSimulator(Scene scene)
    {
        foreach (string prefabName in SimulatorPrefabNames)
        {
            foreach (string guid in AssetDatabase.FindAssets(prefabName + " t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) != prefabName) continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                PrefabUtility.InstantiatePrefab(prefab, scene);
                return true;
            }
        }
        return false;
    }

    static RectTransform CreateUiObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // Legacy UI Text: works without TextMeshPro's essential resources. MenuThemeApplier sets the
    // real fonts, sizes and colours afterwards.
    static Text CreateText(string name, Transform parent, string content, int fontSize)
    {
        var text = CreateUiObject(name, parent).gameObject.AddComponent<Text>();
        text.text = content;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
