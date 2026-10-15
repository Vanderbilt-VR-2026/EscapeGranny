using UnityEngine;
using UnityEngine.InputSystem;

public class ActionDebug : MonoBehaviour
{
    public InputActionProperty turnAction;
    bool last;

    void Update()
    {
        bool enabled = turnAction.action.enabled;
        if (enabled != last)
        {
            Debug.Log($"Turn action enabled changed: {enabled} at frame {Time.frameCount}");
            last = enabled;
        }
    }
}