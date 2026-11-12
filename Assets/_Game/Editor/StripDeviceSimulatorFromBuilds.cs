// StripDeviceSimulatorFromBuilds.cs
//
// Removes XRI's XR Device Simulator (and the newer XR Interaction Simulator) from every scene while a
// headset build is being made. The simulator is for testing in the Editor without a headset (macOS has
// no Quest Link), but if it gets into a build it disconnects the real Quest headset ("Remove Other HMD
// Devices") and uses a fake one parked at floor height: the player starts in the floor and standing up
// does nothing.
//
// Setup: nothing. Keep the simulator in your scene for Editor testing if you like; builds never get it.
// It only changes the copy of the scene that goes into the build, never the scene file itself.

using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

public class StripDeviceSimulatorFromBuilds : IProcessSceneWithReport
{
    public int callbackOrder => 0;

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        // Unity also calls this when you press Play in the Editor, with no build report. Keep the
        // simulator there: that's what it's for.
        if (report == null) return;

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var sim in root.GetComponentsInChildren<XRDeviceSimulator>(true)) Strip(sim, scene);
            foreach (var sim in root.GetComponentsInChildren<XRInteractionSimulator>(true)) Strip(sim, scene);
        }
    }

    static void Strip(Component simulator, Scene scene)
    {
        if (simulator == null) return; // already removed along with its parent
        Debug.Log($"[StripDeviceSimulatorFromBuilds] Removed \"{simulator.gameObject.name}\" from {scene.name} for the build " +
                  "(the simulator would replace the real headset).");
        Object.DestroyImmediate(simulator.gameObject);
    }
}
