// OpenCloseSound.cs
//
// Creak when a door, wardrobe, cabinet or lid starts opening (a wooden slide for drawers), and a
// thud/latch when it finishes closing (when it actually shuts, not when the swing starts). Big doors
// and small cabinet doors get different sounds: Auto picks by size (at least Door Height tall = door).
// Drawers always use the drawer sounds.
//
// Both players hear it: the open/closed state is already shared by NetworkToggle, and each headset
// plays the sound when it sees the state change. (Drawers aren't networked yet, so only the player
// using a drawer sees and hears it.) Distance and floor rules apply (OneShotSound).
//
// Setup: nothing. Door, Lid, DrawerSlide and DrawerPull add this automatically. To force a size or
// use your own clips, Add Component > Open Close Sound on the same object in Edit mode. Empty clips use
// Resources/DoorSounds (Tools > Escape Granny > Generate Door Sounds).

using UnityEngine;

public class OpenCloseSound : MonoBehaviour
{
    public enum Size { Auto, Door, Cabinet, Drawer }

    [SerializeField] Size size = Size.Auto;
    [Tooltip("Auto: if the pivot's model is at least this tall (m) it's a door, otherwise a cabinet.")]
    [SerializeField] float doorHeight = 1.6f;

    [Tooltip("Empty = DoorOpen or CabinetOpen from Resources/DoorSounds.")]
    [SerializeField] AudioClip openSound;
    [Tooltip("Empty = DoorClose or CabinetClose from Resources/DoorSounds.")]
    [SerializeField] AudioClip closeSound;

    [SerializeField, Range(0f, 1f)] float volume = 0.8f;
    [SerializeField] float hearingDistance = 15f;
    [SerializeField, Range(0f, 0.3f)] float pitchVariation = 0.08f;
    [Tooltip("The close sound plays once the door is within this many degrees of shut.")]
    [SerializeField] float closedAngle = 3f;
    [Tooltip("Silent for this many seconds after the scene starts, while doors take their starting positions.")]
    [SerializeField] float quietAtStartSeconds = 1.5f;

    Renderer[] renderers;
    bool clipsLoaded, started, lastOpen, closing;
    float quietUntil;

    /// <summary>Adds an OpenCloseSound (if there isn't one). Called by Door and Lid.</summary>
    public static OpenCloseSound Ensure(GameObject target, Size defaultSize)
    {
        if (target.TryGetComponent<OpenCloseSound>(out var existing)) return existing;
        var added = target.AddComponent<OpenCloseSound>();
        added.size = defaultSize;
        return added;
    }

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        quietUntil = Time.time + quietAtStartSeconds;
    }

    /// <summary>For hinged things (Door, Lid): shut means within Closed Angle degrees of closed.</summary>
    public void Tick(bool isOpen, float degreesFromClosed) => Tick(isOpen, degreesFromClosed <= closedAngle);

    /// <summary>For anything else (drawers): the caller says whether it is fully shut right now.</summary>
    public void Tick(bool isOpen, bool isShut)
    {
        if (!started) { SyncSilently(isOpen); return; }

        if (isOpen != lastOpen)
        {
            lastOpen = isOpen;
            closing = !isOpen;
            if (isOpen) Play(true);
        }

        if (closing && isShut)
        {
            closing = false;
            Play(false);
        }
    }

    /// <summary>Takes on this state without a sound (e.g. when joining online and the shared value arrives).</summary>
    public void SyncSilently(bool isOpen)
    {
        started = true;
        lastOpen = isOpen;
        closing = false;
    }

    void Play(bool opening)
    {
        if (Time.time < quietUntil) return;
        LoadClips(); // the first time: pick door or cabinet sounds
        OneShotSound.PlayAt(opening ? openSound : closeSound, Center(), volume, hearingDistance, 1.5f, pitchVariation);
    }

    void LoadClips()
    {
        if (clipsLoaded) return;
        clipsLoaded = true;
        if (size == Size.Auto) size = MeasureHeight() >= doorHeight ? Size.Door : Size.Cabinet;
        string prefix = size == Size.Door ? "Door" : size == Size.Drawer ? "Drawer" : "Cabinet";
        if (openSound == null) openSound = Resources.Load<AudioClip>($"DoorSounds/{prefix}Open");
        if (closeSound == null) closeSound = Resources.Load<AudioClip>($"DoorSounds/{prefix}Close");
    }

    // Plays from the middle of the door/cabinet model, not the hinge.
    Vector3 Center()
    {
        if (renderers.Length == 0) return transform.position;
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b.center;
    }

    float MeasureHeight()
    {
        if (renderers.Length == 0) return 0f;
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b.size.y;
    }
}
