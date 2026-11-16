// GrannyTutorialPanelBuilder.cs  (goes in an Editor folder, e.g. Assets/_Game/Editor/)
//
// Adds Granny's weapon tutorial screen to the main menu: copies the prisoner's briefing screen (so it
// looks identical), turns the copy into Granny's weapon tutorial, wires its READY/BACK buttons, plugs
// it into MenuController, then adds the video area (video on the left, text on the right).
//
// How to use: nothing. Tools > Escape Granny > Build Main Menu runs it (AddTo) after building the
// menu, and saves the scene. It used to be two separate Tools items that had to be run by hand after
// every rebuild; forgetting them lost the tutorial.

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public static class GrannyTutorialPanelBuilder
{
    const string PanelName = "GrannyTutorialPanel";

    // Builds the tutorial panel and its video area in the given (menu) scene. The caller saves the
    // scene. Returns problems to show the user, or an empty string if everything worked.
    public static string AddTo(Scene scene)
    {
        var problems = new List<string>();
        var tutorial = BuildPanel(scene, problems);
        if (tutorial != null) AddVideos(tutorial, problems);
        return string.Join("\n", problems);
    }

    static GrannyWeaponTutorial BuildPanel(Scene scene, List<string> problems)
    {
        var roots = scene.GetRootGameObjects();
        var menu = roots.SelectMany(r => r.GetComponentsInChildren<MenuController>(true)).FirstOrDefault();
        var brief = roots.SelectMany(r => r.GetComponentsInChildren<PrisonerBriefing>(true)).FirstOrDefault();
        if (menu == null || brief == null)
        {
            problems.Add("- Granny's tutorial was skipped: no MenuController or prisoner briefing panel in the scene.");
            return null;
        }

        var parent = brief.transform.parent;
        var existing = parent.Find(PanelName);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        // Copy the prisoner's screen so the layout and style match exactly.
        bool wasActive = brief.gameObject.activeSelf;
        var panel = Object.Instantiate(brief.gameObject, parent);
        panel.name = PanelName;
        panel.transform.SetSiblingIndex(brief.transform.GetSiblingIndex() + 1);
        brief.gameObject.SetActive(wasActive);

        Object.DestroyImmediate(panel.GetComponent<PrisonerBriefing>());
        var tutorial = panel.AddComponent<GrannyWeaponTutorial>();

        var heading = FindText(panel.transform, "Heading");
        var body = FindText(panel.transform, "BriefCard/BriefingText");
        if (heading != null) heading.text = "HOW TO USE THE WEAPON";
        if (body != null) body.text = "(weapon tutorial text)";

        var so = new SerializedObject(tutorial);
        so.FindProperty("menuController").objectReferenceValue = menu;
        so.FindProperty("headingText").objectReferenceValue = heading;
        so.FindProperty("bodyText").objectReferenceValue = body;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (!Rewire(panel.transform.Find("ReadyButton"), tutorial.OnReadyPressed))
            problems.Add("- Couldn't find the tutorial's ReadyButton; hook its OnClick to GrannyWeaponTutorial.OnReadyPressed by hand.");
        if (!Rewire(panel.transform.Find("BackButton"), tutorial.OnBackPressed))
            problems.Add("- Couldn't find the tutorial's BackButton; hook its OnClick to GrannyWeaponTutorial.OnBackPressed by hand.");
        if (heading == null || body == null)
            problems.Add("- Couldn't find the tutorial's Heading or BriefingText; drag them into GrannyWeaponTutorial by hand.");

        var mso = new SerializedObject(menu);
        mso.FindProperty("grannyTutorialPanel").objectReferenceValue = panel;
        mso.ApplyModifiedPropertiesWithoutUndo();

        panel.SetActive(false);   // MenuController shows it when Granny confirms a weapon
        return tutorial;
    }

    // ---------- Videos ----------

    // Layout with a video (canvas is 960 x 600): video on the left, text card on the right.
    static readonly Vector2 VideoPosition = new Vector2(-208f, -45f);
    static readonly Vector2 VideoSize = new Vector2(448f, 252f);   // 16:9
    static readonly Vector2 CardPositionWithVideo = new Vector2(232f, -45f);
    static readonly Vector2 CardSizeWithVideo = new Vector2(400f, 252f);
    const float VideoBorder = 4f;

    static void AddVideos(GrannyWeaponTutorial tutorial, List<string> problems)
    {
        var panel = tutorial.transform;
        var card = panel.Find("BriefCard") as RectTransform;
        if (card == null)
        {
            problems.Add("- Granny's tutorial videos were skipped: no BriefCard on the tutorial panel.");
            return;
        }

        // Frame (same colour as the text card) with the video inside it.
        var frame = new GameObject("VideoFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
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
        var missing = new List<string>();
        foreach (var (field, file) in new[] { ("axeVideo", "Tutorial_Axe"), ("knifeVideo", "Tutorial_Knife"),
                                              ("gunVideo", "Tutorial_Gun"), ("bombVideo", "Tutorial_Bomb") })
        {
            var clip = FindClip(file);
            if (clip != null) so.FindProperty(field).objectReferenceValue = clip;
            else missing.Add(file);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        if (missing.Count > 0)
            problems.Add("- Tutorial videos not found (those weapons show text only): " + string.Join(", ", missing));

        frame.SetActive(false);   // the tutorial shows it when the picked weapon has a video
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
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(button.onClick, i);
        UnityEventTools.AddPersistentListener(button.onClick, action);
        return true;
    }
}
#endif
