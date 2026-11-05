using Fusion;
using UnityEngine;

/// <summary>
/// Swaps the Network Player's dummy body for the Granny or Prisoner model, so the other player sees
/// who they're up against.
/// The owner decides the role when the avatar spawns and shares it; every headset then shows the
/// matching model and keeps it standing under the player's (already synced) head. No extra position
/// syncing is needed.
/// Role: GameSession.SelectedRole if the menu set one. The networked lobby has no role menu yet, so
/// until it does, the first player in the room (the master client) is Granny and the other the Prisoner.
/// Setup: put this on the Network Player root (next to FusionNetworkPlayer). Drag the Head child into
/// Head, the two models (children of Network Player) into Granny Body / Prisoner Body, and the dummy
/// parts to hide (Neck, and Head's visual) into Dummy Parts. Hands stay as they are.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NetworkAvatarBody : NetworkBehaviour
{
    [SerializeField] private Transform head;
    [SerializeField] private GameObject grannyBody;
    [SerializeField] private GameObject prisonerBody;
    [Tooltip("Dummy shapes hidden once a real body is showing (e.g. Neck, the head sphere). Not the hands.")]
    [SerializeField] private GameObject[] dummyParts;
    [Tooltip("Height of the model's eyes above its feet (m). 0 = measure it from the model (94% of its height).")]
    [SerializeField, Min(0f)] private float eyeHeight = 0f;

    // The shared role. Fusion copies it to the other player; OnChangedRender swaps the model when it arrives.
    [Networked, OnChangedRender(nameof(ApplyRole))]
    public PlayerRole Role { get; private set; }

    private GameObject activeBody;
    private float activeEyeHeight;
    // Renderers kept switched off: the dummy shapes everywhere, plus our own body on our own headset.
    // Re-checked every frame because PlayerHideState switches all renderers back on when hiding ends.
    private readonly System.Collections.Generic.List<Renderer> keepHidden = new System.Collections.Generic.List<Renderer>();

    public override void Spawned()
    {
        if (HasStateAuthority) Role = PickRole();
        ApplyRole();
    }

    private PlayerRole PickRole()
    {
        if (GameSession.Instance != null && GameSession.Instance.SelectedRole != PlayerRole.None)
            return GameSession.Instance.SelectedRole;
        return Runner.IsSharedModeMasterClient ? PlayerRole.Granny : PlayerRole.Prisoner;
    }

    private void ApplyRole()
    {
        if (grannyBody != null) grannyBody.SetActive(Role == PlayerRole.Granny);
        if (prisonerBody != null) prisonerBody.SetActive(Role == PlayerRole.Prisoner);
        activeBody = Role == PlayerRole.Granny ? grannyBody : Role == PlayerRole.Prisoner ? prisonerBody : null;
        if (activeBody == null) return;

        activeEyeHeight = eyeHeight > 0f ? eyeHeight : MeasureEyeHeight(activeBody);

        // The dummy shapes would poke out of the model, so hide them everywhere.
        keepHidden.Clear();
        foreach (var part in dummyParts)
            if (part != null) keepHidden.AddRange(part.GetComponentsInChildren<Renderer>(true));

        // On our own headset the model would block the camera, so only the other player sees it.
        if (HasStateAuthority) keepHidden.AddRange(activeBody.GetComponentsInChildren<Renderer>(true));

        Debug.Log($"[NetworkAvatarBody] Player {Object.StateAuthority} is {Role}");
    }

    // Every frame on every headset: stand the model under the head, turned the way the head faces
    // (left/right only, so looking down doesn't tip the body over).
    public override void Render()
    {
        if (activeBody == null || head == null) return;
        foreach (var r in keepHidden) if (r != null && r.enabled) r.enabled = false;

        var body = activeBody.transform;
        body.position = head.position - Vector3.up * activeEyeHeight;
        body.rotation = Quaternion.Euler(0f, head.eulerAngles.y, 0f);
    }

    private static float MeasureEyeHeight(GameObject model)
    {
        var renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return 1.6f;
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b.size.y * 0.94f;
    }
}
