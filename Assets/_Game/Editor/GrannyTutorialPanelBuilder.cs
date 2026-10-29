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
