using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

/// <summary>
/// One place that sets how fast THIS headset's player walks (the XR rig's move provider).
/// Several things slow the player down at once (being Granny, carrying a heavy weapon, being injured),
/// so each sets its own multiplier here and the walk speed is the rig's normal speed times all of them.
/// Setting the move provider's speed directly from several scripts would make them undo each other.
/// Setup: nothing. Used by NetworkAvatarBody, WeaponCarrySpeed and PrisonerHealth.
/// </summary>
public static class PlayerSpeed
{
    private static ContinuousMoveProvider mover;
    private static float baseSpeed;
    private static float role = 1f, weapon = 1f, injured = 1f;

    public static void SetRole(float multiplier) { role = multiplier; Apply(); }
    public static void SetWeapon(float multiplier) { weapon = multiplier; Apply(); }
    public static void SetInjured(float multiplier) { injured = multiplier; Apply(); }

    // New round: forget anything left over from the last one (these values survive scene loads).
    public static void ResetAll()
    {
        role = weapon = injured = 1f;
        Apply();
    }

    private static void Apply()
    {
        // The rig is replaced on every scene load, so look it up again (and its normal speed) when it's gone.
        if (mover == null)
        {
            mover = Object.FindFirstObjectByType<ContinuousMoveProvider>();
            if (mover == null) return;
            baseSpeed = mover.moveSpeed;
        }
        mover.moveSpeed = baseSpeed * role * weapon * injured;
    }
}
