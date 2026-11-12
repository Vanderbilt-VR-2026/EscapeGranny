using UnityEngine;

/// <summary>
/// The invisible body-shaped trigger that weapons hit. PrisonerHealth creates it at runtime and
/// keeps it on the Prisoner's body, so there is nothing to set up.
/// Weapons call PlayerHitbox.TryHit(collider, damage) when they touch something.
/// </summary>
public class PlayerHitbox : MonoBehaviour
{
    public PrisonerHealth Health { get; set; }

    // True if the collider is a player's hitbox (the hit was passed on to that player's health).
    public static bool TryHit(Collider other, int damage)
    {
        var hitbox = other.GetComponent<PlayerHitbox>();
        if (hitbox == null || hitbox.Health == null) return false;
        hitbox.Health.TakeHit(damage);
        return true;
    }
}
