using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;

// PUT THIS ON THE XR ORIGIN IN YOUR SCENE (not on the prefab).
public class SceneRigReferences : MonoBehaviour
{
    public XROrigin xrOrigin;
    public CharacterController characterController;
    public Behaviour[] locomotionToDisable; // Dynamic Move Provider + Snap Turn Provider
    public InputActionProperty exitAction;  // e.g. XRI Right/Primary Button
}
