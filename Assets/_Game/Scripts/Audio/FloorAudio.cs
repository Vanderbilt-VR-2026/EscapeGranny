// FloorAudio.cs
//
// The house's "sound through floors" rule, shared by footsteps and every interactable sound:
//   - Same floor as the listener: normal (only distance makes it quieter).
//   - On a floor above the listener: quieter and muffled, like hearing it through the floorboards.
//     Two floors up is quieter still.
//   - On a floor below the listener: not heard at all.
// The listener is this headset's AudioListener (on the Main Camera), so each player's headset applies
// the rule from where that player is standing.
//
// Setup: nothing. Footsteps and OneShotSound call it. Tune the numbers below; they match HouseBuilder
// (floors at y = -3, 0 and 3).

using UnityEngine;

public static class FloorAudio
{
    /// <summary>Height (y) of the ground floor.</summary>
    public static float GroundFloorY = 0f;
    /// <summary>Height of one storey.</summary>
    public static float FloorHeight = 3f;
    /// <summary>Volume multiplier per floor for sounds from above (0.4 = 40%).</summary>
    public static float FromAboveVolume = 0.4f;
    /// <summary>Sounds from above lose everything higher than this pitch (Hz), which sounds muffled.</summary>
    public static float FromAboveMuffleHz = 900f;

    // Things lying on the floor (or a contact point a hair below it) still count as that floor.
    const float FloorTolerance = 0.25f;

    static AudioListener listener;

    /// <summary>Which storey a height is on: 0 = ground, 1 = upper, -1 = basement.</summary>
    public static int FloorOf(float y) => Mathf.FloorToInt((y - GroundFloorY + FloorTolerance) / FloorHeight);

    /// <summary>
    /// How a sound at this position should play for this headset. False = can't be heard (it's below).
    /// Otherwise volumeMultiplier is 1 on the same floor, less from above, and muffled is true from above.
    /// </summary>
    public static bool CanHear(Vector3 soundPosition, out float volumeMultiplier, out bool muffled)
    {
        volumeMultiplier = 1f;
        muffled = false;

        if (listener == null || !listener.isActiveAndEnabled) listener = Object.FindFirstObjectByType<AudioListener>();
        if (listener == null) return true; // nothing to compare against: just play it

        int floorsAbove = FloorOf(soundPosition.y) - FloorOf(listener.transform.position.y);
        if (floorsAbove < 0) return false;
        if (floorsAbove > 0)
        {
            volumeMultiplier = Mathf.Pow(FromAboveVolume, floorsAbove);
            muffled = true;
        }
        return true;
    }
}
