// GrannyTutorialPanelBuilder.cs  (goes in an Editor folder, e.g. Assets/_Game/Editor/)
//
// Open MainMenu, then: Tools > Escape Granny > Build Granny Weapon Tutorial Panel
// Copies the prisoner's briefing screen (so it looks identical), turns the copy into Granny's
// weapon tutorial, wires its READY/BACK buttons, and plugs it into MenuController.
// Nothing is saved automatically: check it, then press Cmd+S.

#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public static class GrannyTutorialPanelBuilder
{
    const string PanelName = "GrannyTutorialPanel";

    [MenuItem("Tools/Escape Granny/Build Granny Weapon Tutorial Panel")]
    static void Build()
    {
        var scene = SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();

        var menu = roots.SelectMany(r => r.GetComponentsInChildren<MenuController>(true)).FirstOrDefault();
        var brief = roots.SelectMany(r => r.GetComponentsInChildren<PrisonerBriefing>(true)).FirstOrDefault();
        if (menu == null || brief == null)
        {
            Dialog("Open the MainMenu scene first (it needs MenuController and the prisoner's briefing panel).");
            return;
        }

        var parent = brief.transform.parent;
        var existing = parent.Find(PanelName);
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("Escape Granny",
                    $"{PanelName} already exists. Replace it with a fresh copy of the prisoner's screen?", "Replace", "Cancel"))
                return;
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        // Copy the prisoner's screen so the layout and style match exactly.
        bool wasActive = brief.gameObject.activeSelf;
        var panel = Object.Instantiate(brief.gameObject, parent);
        Undo.RegisterCreatedObjectUndo(panel, "Build Granny Weapon Tutorial Panel");
        panel.name = PanelName;
        panel.transform.SetSiblingIndex(brief.transform.GetSiblingIndex() + 1);
        brief.gameObject.SetActive(wasActive);

        Object.DestroyImmediate(panel.GetComponent<PrisonerBriefing>());
        var tutorial = panel.AddComponent<GrannyWeaponTutorial>();

        var heading = FindText(panel.transform, "Heading");
        var body = FindText(panel.transform, "BriefCard/BriefingText");
        var step = FindText(panel.transform, "Step");
        if (step != null) step.text = "GET READY";
        if (heading != null) heading.text = "HOW TO USE THE WEAPON";
        if (body != null) body.text = "(weapon tutorial text)";

        var so = new SerializedObject(tutorial);
        so.FindProperty("menuController").objectReferenceValue = menu;
        so.FindProperty("headingText").objectReferenceValue = heading;
        so.FindProperty("bodyText").objectReferenceValue = body;
        so.ApplyModifiedPropertiesWithoutUndo();

        bool readyOk = Rewire(panel.transform.Find("ReadyButton"), tutorial.OnReadyPressed);
        bool backOk = Rewire(panel.transform.Find("BackButton"), tutorial.OnBackPressed);

        var mso = new SerializedObject(menu);
        var field = mso.FindProperty("grannyTutorialPanel");
        if (field == null)
        {
            Dialog("MenuController doesn't have a Granny Tutorial Panel field yet. Replace MenuController.cs with " +
                   "the updated version, let Unity recompile, then run this again.");
            Undo.DestroyObjectImmediate(panel);
            return;
        }
        field.objectReferenceValue = panel;
        mso.ApplyModifiedProperties();

        panel.SetActive(false);   // MenuController shows it when Granny confirms a weapon
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = panel;

        string warn = "";
        if (heading == null || body == null) warn += "\n- Couldn't find the Heading or BriefingText on the copy; drag them into GrannyWeaponTutorial by hand.";
        if (!readyOk) warn += "\n- Couldn't find ReadyButton; hook its OnClick to GrannyWeaponTutorial.OnReadyPressed by hand.";
        if (!backOk) warn += "\n- Couldn't find BackButton; hook its OnClick to GrannyWeaponTutorial.OnBackPressed by hand.";

        Dialog("Built Granny's weapon tutorial screen (a copy of the prisoner's briefing), wired READY and BACK, " +
               "and added it to MenuController." + warn +
               "\n\nThe text for each weapon is on the GrannyWeaponTutorial component if you want to edit it." +
               "\n\nPress Cmd+S to save.");
    }

    // ---------- Videos ----------

    // Layout with a video (canvas is 960 x 600): video on the left, text card on the right.
    static readonly Vector2 VideoPosition = new Vector2(-208f, -45f);
    static readonly Vector2 VideoSize = new Vector2(448f, 252f);   // 16:9
    static readonly Vector2 CardPositionWithVideo = new Vector2(232f, -45f);
    static readonly Vector2 CardSizeWithVideo = new Vector2(400f, 252f);
    const float VideoBorder = 4f;

    [MenuItem("Tools/Escape Granny/Add Videos To Granny Tutorial")]
    static void AddVideos()
    {
        var scene = SceneManager.GetActiveScene();
        var tutorial = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<GrannyWeaponTutorial>(true))
            .FirstOrDefault();
        if (tutorial == null)
        {
            Dialog("Couldn't find Granny's tutorial panel. Open MainMenu and run " +
                   "\"Build Granny Weapon Tutorial Panel\" first.");
            return;
        }

        var panel = tutorial.transform;
        var card = panel.Find("BriefCard") as RectTransform;
        if (card == null)
        {
            Dialog("Couldn't find BriefCard on the tutorial panel.");
            return;
        }

        var old = panel.Find("VideoFrame");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        // Frame (same colour as the text card) with the video inside it.
        var frame = new GameObject("VideoFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(frame, "Add Videos To Granny Tutorial");
        var frameRect = (RectTransform)frame.transform;
        frameRect.SetParent(panel, false);
        frameRect.SetSiblingIndex(card.GetSiblingIndex() + 1);
        frameRect.anchorMin = frameRect.anchorMax = new Vector2(0.5f, 0.5f);
        frameRect.pivot = new Vector2(0.5f, 0.5f);
        frameRect.anchoredPosition = VideoPosition;
        frameRect.sizeDelta = VideoSize;
        var frameImage = frame.GetComponent<Image>();
        var cardImage = card.GetComponent<Image>();
        frameImage.color = cardImage != null ? cardImage.color : Color.black;
        frameImage.raycastTarget = false;

        var video = new GameObject("Video", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(VideoPlayer));
        var videoRect = (RectTransform)video.transform;
        videoRect.SetParent(frameRect, false);
        videoRect.anchorMin = Vector2.zero;
        videoRect.anchorMax = Vector2.one;
        videoRect.offsetMin = new Vector2(VideoBorder, VideoBorder);
        videoRect.offsetMax = new Vector2(-VideoBorder, -VideoBorder);
        var raw = video.GetComponent<RawImage>();
        raw.color = Color.white;
        raw.raycastTarget = false;
        var player = video.GetComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.isLooping = true;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.audioOutputMode = VideoAudioOutputMode.None;
        player.waitForFirstFrame = true;
        player.skipOnDrop = true;

        // Let the body text shrink to fit the narrower card.
        var body = card.Find("BriefingText")?.GetComponent<Text>();
        if (body != null)
        {
            Undo.RecordObject(body, "Add Videos To Granny Tutorial");
            body.resizeTextForBestFit = true;
            body.resizeTextMinSize = 16;
            body.resizeTextMaxSize = Mathf.Max(body.fontSize, 26);
        }

        var so = new SerializedObject(tutorial);
        so.FindProperty("videoFrame").objectReferenceValue = frame;
        so.FindProperty("videoImage").objectReferenceValue = raw;
        so.FindProperty("videoPlayer").objectReferenceValue = player;
        so.FindProperty("textCard").objectReferenceValue = card;
        so.FindProperty("cardPositionWithVideo").vector2Value = CardPositionWithVideo;
        so.FindProperty("cardSizeWithVideo").vector2Value = CardSizeWithVideo;

        // Fill in any clips named Tutorial_Axe / _Knife / _Gun / _Bomb that are in the project.
        var found = new System.Collections.Generic.List<string>();
        var missing = new System.Collections.Generic.List<string>();
        foreach (var (field, file) in new[] { ("axeVideo", "Tutorial_Axe"), ("knifeVideo", "Tutorial_Knife"),
                                              ("gunVideo", "Tutorial_Gun"), ("bombVideo", "Tutorial_Bomb") })
        {
            var prop = so.FindProperty(field);
            var clip = FindClip(file);
            if (clip != null) { prop.objectReferenceValue = clip; found.Add(file); }
            else if (prop.objectReferenceValue == null) missing.Add(file);
        }
        so.ApplyModifiedProperties();

        frame.SetActive(false);   // the tutorial shows it when the picked weapon has a video
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = tutorial.gameObject;

        Dialog("Added a video area to Granny's tutorial screen (video on the left, text on the right)." +
               (found.Count > 0 ? "\n\nVideos found: " + string.Join(", ", found) : "") +
               (missing.Count > 0 ? "\n\nNot found (those weapons show text only): " + string.Join(", ", missing) +
                                    "\nPut the .mp4 files in the project, or drag clips into GrannyWeaponTutorial." : "") +
               "\n\nPress Cmd+S to save.");
    }

    static VideoClip FindClip(string fileName)
    {
        return AssetDatabase.FindAssets(fileName + " t:VideoClip")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => System.IO.Path.GetFileNameWithoutExtension(p) == fileName)
            .Select(AssetDatabase.LoadAssetAtPath<VideoClip>)
            .FirstOrDefault(c => c != null);
    }

    static Text FindText(Transform root, string path)
    {
        var t = root.Find(path);
        return t != null ? t.GetComponent<Text>() : null;
    }

    // Replaces a button's OnClick list with a single call.
    static bool Rewire(Transform t, UnityAction action)
    {
        var button = t != null ? t.GetComponent<Button>() : null;
        if (button == null) return false;
        Undo.RecordObject(button, "Rewire button");
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(button.onClick, i);
        UnityEventTools.AddPersistentListener(button.onClick, action);
        EditorUtility.SetDirty(button);
        return true;
    }

    static void Dialog(string msg) => EditorUtility.DisplayDialog("Escape Granny", msg, "OK");
}
#endif
