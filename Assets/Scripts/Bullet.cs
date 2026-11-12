using UnityEngine;

/// <summary>
/// Put on the Bullet prefab. Spawns an impact flash where it hits, then disappears.
/// If it passes through the Prisoner's hitbox it hurts them (Damage is set by the Gun that fired it).
/// </summary>
public class Bullet : MonoBehaviour
{
    [SerializeField] GameObject impactPrefab;
    [SerializeField] float lifetime = 5f;
    [SerializeField] float pushForce = 1.5f;

    // Hits taken off the Prisoner. 0 for the copy of the bullet on the other player's headset.
    public int Damage { get; set; }

    bool hit;
    Vector3 lastPosition;
    readonly RaycastHit[] sweepHits = new RaycastHit[8];

    void Start()
    {
        lastPosition = transform.position;
        Destroy(gameObject, lifetime);
    }

    // The hitbox is a trigger, and a fast bullet can jump right over it between two physics steps,
    // so check the whole path it travelled this step instead.
    void FixedUpdate()
    {
        if (hit) return;
        Vector3 step = transform.position - lastPosition;
        int count = step.sqrMagnitude > 0f
            ? Physics.RaycastNonAlloc(lastPosition, step.normalized, sweepHits, step.magnitude, ~0, QueryTriggerInteraction.Collide)
            : 0;
        lastPosition = transform.position;

        for (int i = 0; i < count; i++)
        {
            if (sweepHits[i].collider.GetComponent<PlayerHitbox>() == null) continue;
            hit = true;
            if (Damage > 0) PlayerHitbox.TryHit(sweepHits[i].collider, Damage, sweepHits[i].point);
            if (impactPrefab != null) Instantiate(impactPrefab, sweepHits[i].point, Quaternion.identity);
            Destroy(gameObject);
            return;
        }
    }

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
