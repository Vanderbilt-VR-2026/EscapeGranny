using UnityEngine;

// Make this world-space UI face the camera assigned in the Inspector (or the main camera if none is).
public class CanvasController : MonoBehaviour
{
    public GameObject Camera;

    // No startup setup is needed here; the camera reference is already assigned in the Inspector.
    void Start()
    {
        
    }

    // Reorient every Unity frame so the text remains readable as the viewer moves.
    void Update()
    {
        // No camera assigned in the Inspector: use the scene's main camera, once there is one.
        if (Camera == null)
        {
            if (UnityEngine.Camera.main == null) return;
            Camera = UnityEngine.Camera.main.gameObject;
        }
        // LookAt points this transform's forward axis at the camera.
        this.transform.LookAt(Camera.transform);
        // This canvas's readable side faces the opposite way, so turn it half a rotation around local Y.
        this.transform.Rotate(0, 180, 0);
    }
}
