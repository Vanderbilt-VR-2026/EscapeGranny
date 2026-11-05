using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

// Put this on the EscapeTrigger object in House_Bishal (by the front door).
// When the player walks through, it loads the success scene once.
[RequireComponent(typeof(Collider))]
public class EscapeSuccessTrigger : MonoBehaviour
{
    [Tooltip("Scene to load when the player escapes. Must be listed in File > Build Settings.")]
    public string successSceneName = "Success_Scene";

    [Tooltip("Seconds to wait before loading (e.g. to let a sound play).")]
    public float delay = 0f;

    [Tooltip("Optional extra things to do the moment the player escapes.")]
    public UnityEvent onEscaped;

    bool hasFired;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasFired) return;

        // Only react to the player's XR rig, not Granny or thrown objects.
        if (other.GetComponentInParent<XROrigin>() == null) return;

        hasFired = true;
        Debug.Log("[EscapeSuccessTrigger] Player escaped, loading " + successSceneName);
        onEscaped.Invoke();

        if (!string.IsNullOrEmpty(successSceneName))
            Invoke(nameof(LoadSuccessScene), delay);
    }

    void LoadSuccessScene()
    {
        SceneManager.LoadScene(successSceneName);
    }
}
