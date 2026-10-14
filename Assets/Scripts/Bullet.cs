using UnityEngine;

/// <summary>Put on the Bullet prefab. Spawns an impact flash where it hits, then disappears.</summary>
public class Bullet : MonoBehaviour
{
    [SerializeField] GameObject impactPrefab;
    [SerializeField] float lifetime = 5f;
    [SerializeField] float pushForce = 1.5f;

    bool hit;

    void Start() => Destroy(gameObject, lifetime);

    void OnCollisionEnter(Collision collision)
    {
        if (hit) return;
        hit = true;

        ContactPoint cp = collision.GetContact(0);

        if (impactPrefab != null)
            Instantiate(impactPrefab, cp.point, Quaternion.LookRotation(cp.normal));

        if (collision.rigidbody != null)
            collision.rigidbody.AddForceAtPosition(-cp.normal * pushForce, cp.point, ForceMode.Impulse);

        Destroy(gameObject);
    }
}
