// QuestPerformance.cs
//
// Keeps the game at the Quest's 72 frames per second. Measured on a Quest 3 the house ran at ~28 fps
// with the graphics chip (GPU) maxed out; these are the biggest savings that need no scene editing:
//
//   1. The outdoor landscape copied from the success scene ("Nature (from Success_Scene)": terrain,
//      trees, cliffs, rocks and its own sun) is switched off until the escape door opens. Nobody can
//      see outside before that (the house has no windows), but it was drawn every frame anyway.
//   2. Fixed foveated rendering: the edges of each eye's view are drawn at lower resolution, where
//      the lenses blur them anyway. "Dynamic" means it only goes as far as needed to keep 72 fps.
//   3. No real-time shadows from any light (the project already says "shadows off on Quest"): even if
//      a light in a scene still has shadows ticked, the extra shadow drawing pass is skipped.
//
// Setup: nothing. It starts itself in every scene, in builds and in the Editor. To turn one of these
// off, change the matching constant below.

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class QuestPerformance : MonoBehaviour
{
    private const string OutdoorsRootName = "Nature (from Success_Scene)";
    private static readonly bool HideOutdoorsUntilEscape = true;
    private static readonly bool UseFoveatedRendering = true;
    private static readonly bool TurnOffRealtimeShadows = true;

    private static QuestPerformance instance;

    private GameObject outdoors;
    private EscapeDoor escapeDoor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        if (instance != null) return;
        instance = new GameObject("QuestPerformance").AddComponent<QuestPerformance>();
        DontDestroyOnLoad(instance.gameObject);
        SceneManager.sceneLoaded += (scene, mode) => instance.SetUpScene(scene);
        instance.SetUpScene(SceneManager.GetActiveScene());
        if (UseFoveatedRendering) instance.StartCoroutine(instance.TurnOnFoveation());
    }

    private void SetUpScene(Scene scene)
    {
        if (TurnOffRealtimeShadows)
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                light.shadows = LightShadows.None;

        if (!HideOutdoorsUntilEscape) return;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name != OutdoorsRootName || !root.activeSelf) continue;
            // Only hide it if there's an escape door to bring it back; otherwise leave it alone.
            escapeDoor = FindFirstObjectByType<EscapeDoor>();
            if (escapeDoor == null) return;
            outdoors = root;
            outdoors.SetActive(false);
            Debug.Log("[QuestPerformance] Outdoors hidden until the escape door opens.");
        }
    }

    private void Update()
    {
        // Show the outdoors the moment the escape door opens, so the player sees it through the doorway.
        if (outdoors == null || outdoors.activeSelf) return;
        if (escapeDoor == null || escapeDoor.IsOpen)
        {
            outdoors.SetActive(true);
            Debug.Log("[QuestPerformance] Escape door open: outdoors shown.");
        }
    }

    // The OpenXR session only exists once the headset has started, so try for a few seconds.
    private IEnumerator TurnOnFoveation()
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            yield return new WaitForSecondsRealtime(0.5f);
            try
            {
                Meta.XR.MetaXRFoveationFeature.foveatedRenderingLevel = OVRManager.FoveatedRenderingLevel.High;
                Meta.XR.MetaXRFoveationFeature.useDynamicFoveatedRendering = true;
                if (Meta.XR.MetaXRFoveationFeature.foveatedRenderingLevel != OVRManager.FoveatedRenderingLevel.Off)
                {
                    Debug.Log("[QuestPerformance] Foveated rendering on (High, dynamic).");
                    yield break;
                }
            }
            catch (System.Exception e)
            {
                // No headset (e.g. Editor on a Mac) or the feature isn't available: nothing to do.
                Debug.Log($"[QuestPerformance] Foveated rendering not available here: {e.Message}");
                yield break;
            }
        }
    }
}
