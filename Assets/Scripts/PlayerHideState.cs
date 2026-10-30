using Fusion;
using UnityEngine;

// PUT THIS ON THE NETWORK PLAYER PREFAB ROOT.
public class PlayerHideState : NetworkBehaviour
{
    [Networked] public NetworkBool IsHidden { get; set; }

    [SerializeField] Collider[] hitColliders;   // colliders the granny can hit/grab (NOT the Character Controller)
    [SerializeField] AudioSource speaker;       // AudioSource on the "Speaker" object, if it has one

    Renderer[] avatarRenderers;
    ChangeDetector _changes;

    public override void Spawned()
    {
        avatarRenderers = GetComponentsInChildren<Renderer>(true);
        _changes = GetChangeDetector(ChangeDetector.Source.SimulationState);
        Apply(IsHidden);
    }

    public override void Render()
    {
        foreach (var change in _changes.DetectChanges(this))
            if (change == nameof(IsHidden)) Apply(IsHidden);
    }

    public void SetHidden(bool hidden)
    {
        if (HasStateAuthority) IsHidden = hidden;
    }
    
    void Apply(bool hidden)
    {
        // The owner keeps seeing their own hands; everyone else loses sight of them
        if (!HasInputAuthority)
        {
            foreach (var r in avatarRenderers) r.enabled = !hidden;
            if (speaker != null) speaker.mute = hidden;
        }
        foreach (var c in hitColliders) c.enabled = !hidden;
    }
}
