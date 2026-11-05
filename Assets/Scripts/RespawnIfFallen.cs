using UnityEngine;

// Put this on anything the player can drop (e.g. Key_Root).
//  - Makes its physics check collisions continuously, so it doesn't slip through thin floors.
//  - If it still ends up below the house, it's put back where it started.
[DisallowMultipleComponent]
public class RespawnIfFallen : MonoBehaviour
{
    [Tooltip("If the object drops below this height, it's put back. The basement floor is at about -3, so -6 is safely under everything.")]
    public float fallHeight = -6f;

    [Tooltip("Optional spot to respawn at. Leave empty to use where the object starts.")]
    public Transform respawnPoint;

    Vector3 startPos;
    Quaternion startRot;
    Rigidbody rb;

    void Awake()
    {
        startPos = transform.position;
        startRot = transform.rotation;
        rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.collisionDetectionMode = rb.isKinematic
                ? CollisionDetectionMode.ContinuousSpeculative
                : CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        bool hasSolidCollider = false;
        foreach (var c in GetComponentsInChildren<Collider>())
            if (!c.isTrigger) { hasSolidCollider = true; break; }
        if (!hasSolidCollider)
            Debug.LogWarning($"[RespawnIfFallen] {name} has no solid (non-trigger) collider, so it will fall through floors. " +
                             "Add a Box Collider with 'Is Trigger' unticked.", this);
    }

    void FixedUpdate()
    {
        if (transform.position.y < fallHeight) Respawn();
    }

    public void Respawn()
    {
        Vector3 pos = respawnPoint != null ? respawnPoint.position : startPos;
        Quaternion rot = respawnPoint != null ? respawnPoint.rotation : startRot;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = pos;
            rb.rotation = rot;
        }
        transform.SetPositionAndRotation(pos, rot);
        Debug.Log($"[RespawnIfFallen] {name} fell out of the world and was put back.", this);
    }
}
