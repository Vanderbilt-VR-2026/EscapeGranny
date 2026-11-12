using Fusion;
using UnityEngine;

/// <summary>
/// Swaps the Network Player's dummy body for the Granny or Prisoner model, so the other player sees
/// who they're up against.
/// The owner decides the role when the avatar spawns and shares it; every headset then shows the
/// matching model and keeps it standing on the floor under the player's (already synced) head. No
/// extra position syncing is needed. Feet go on the floor, not a fixed distance below the head, so a
/// crouching player (or the Editor with no headset, whose head sits at floor level) doesn't sink
/// through the floor.
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
    [Tooltip("Granny's height in the game (m). The model is scaled to this, whatever size its file is. 0 = keep the file's size.")]
    [SerializeField, Min(0f)] private float grannyHeight = 1.65f;
    [Tooltip("The Prisoner's height in the game (m). 0 = keep the file's size.")]
    [SerializeField, Min(0f)] private float prisonerHeight = 1.75f;
    [Tooltip("Granny's walk speed compared to the Prisoner's (the game rules say 0.75).")]
    [SerializeField, Range(0.3f, 1.5f)] private float grannySpeed = 0.75f;
    [Tooltip("How far down from the head to look for the floor (m).")]
    [SerializeField, Min(1f)] private float floorSearchDistance = 4f;

    // The shared role. Fusion copies it to the other player; OnChangedRender swaps the model when it arrives.
    [Networked, OnChangedRender(nameof(ApplyRole))]
    public PlayerRole Role { get; private set; }

    // Point on the floor under this player's head, updated every frame. PrisonerHealth puts the hitbox here.
    public Vector3 FeetPosition { get; private set; }

    [Tooltip("Seconds the body takes to fall to the floor when the Prisoner dies.")]
    [SerializeField, Min(0.1f)] private float fallSeconds = 0.8f;

    private GameObject activeBody;
    private bool fallen; // dead: the body lies where it fell instead of following the head
    private float pivotAboveFeet; // the model's pivot can be at its feet or its middle
    private readonly RaycastHit[] floorHits = new RaycastHit[8];
    // Renderers kept switched off: the dummy shapes everywhere, plus our own body on our own headset.
    // Re-checked every frame because PlayerHideState switches all renderers back on (see LateUpdate).
    private readonly System.Collections.Generic.List<Renderer> keepHidden = new System.Collections.Generic.List<Renderer>();

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            Role = PickRole();
            PlayerSpeed.ResetAll(); // new round: nothing carried over from the last one
        }
        ApplyRole();
    }

    private PlayerRole PickRole()
    {
        // Networked start menu: the role this player claimed there.
        var select = NetworkRoleSelect.Instance;
        if (select != null && select.IsReady)
        {
            var claimed = select.RoleOf(Runner.LocalPlayer);
            if (claimed != PlayerRole.None) return claimed;
        }
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

        ScaleToHeight(activeBody, Role == PlayerRole.Granny ? grannyHeight : prisonerHeight);
        pivotAboveFeet = MeasurePivotAboveFeet(activeBody);

        // Imported character meshes can carry wrong bounds, so Unity thinks they're off-screen and
        // skips drawing them. Always draw the body (it's only one model per player).
        foreach (var skinned in activeBody.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            skinned.updateWhenOffscreen = true;

        // The dummy shapes would poke out of the model, so hide them everywhere.
        keepHidden.Clear();
        foreach (var part in dummyParts)
            if (part != null) keepHidden.AddRange(part.GetComponentsInChildren<Renderer>(true));

        if (HasStateAuthority) PlayerSpeed.SetRole(Role == PlayerRole.Granny ? grannySpeed : 1f);

        // On our own headset the model would block the camera, so only the other player sees it.
        if (HasStateAuthority) keepHidden.AddRange(activeBody.GetComponentsInChildren<Renderer>(true));

        Debug.Log($"[NetworkAvatarBody] Player {Object.StateAuthority} is {Role}, model {MeasureBounds(activeBody).size.y:0.00} m tall");
    }

    // Every frame on every headset: stand the model on the floor under the head, turned the way the
    // head faces (left/right only, so looking down doesn't tip the body over).
    public override void Render()
    {
        if (activeBody == null || head == null || fallen) return;

        var body = activeBody.transform;
        FeetPosition = new Vector3(head.position.x, FloorBelowHead(), head.position.z);
        body.position = FeetPosition + Vector3.up * pivotAboveFeet;
        body.rotation = Quaternion.Euler(0f, head.eulerAngles.y, 0f);
    }

    // Death (PrisonerHealth calls this on both headsets): the body stops following the head and tips
    // over backwards around its feet, speeding up like a real fall, then stays on the floor.
    public void FallOver()
    {
        if (fallen || activeBody == null) return;
        fallen = true;
        StartCoroutine(FallRoutine());
    }

    private System.Collections.IEnumerator FallRoutine()
    {
        var body = activeBody.transform;
        var animator = activeBody.GetComponentInChildren<Animator>();
        if (animator != null) animator.enabled = false; // freeze the idle pose so it doesn't fight the fall

        Vector3 feet = FeetPosition;
        Vector3 axis = body.right; // falling backwards = rotating around the body's left-right line
        float turned = 0f;
        for (float t = 0f; t < fallSeconds; t += Time.deltaTime)
        {
            float target = 90f * Mathf.Pow(t / fallSeconds, 2f); // slow start, fast finish
            body.RotateAround(feet, axis, -(target - turned));
            turned = target;
            yield return null;
        }
        body.RotateAround(feet, axis, -(90f - turned));
    }

    // PlayerHideState switches every renderer under Avatar Visuals back on each frame (in Render), so
    // hide ours again in LateUpdate, which always runs after Fusion's Render. Only ever switches off,
    // so hiding a Prisoner from the other player still works.
    private void LateUpdate()
    {
        foreach (var r in keepHidden) if (r != null && r.enabled) r.enabled = false;
    }

    // Height of the nearest floor under the head. Starts a little above the head, because without a
    // headset (Editor) the head can sit exactly at floor level and a ray starting there misses the floor.
    // Skips this player's own colliders (hands) and triggers.
    private float FloorBelowHead()
    {
        Vector3 from = head.position + Vector3.up * 0.3f;
        int count = Physics.RaycastNonAlloc(from, Vector3.down, floorHits, floorSearchDistance, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue, floorY = head.position.y - 1.6f; // no floor found: assume standing
        for (int i = 0; i < count; i++)
        {
            var hit = floorHits[i];
            if (hit.transform.IsChildOf(transform) || hit.distance >= best) continue;
            best = hit.distance;
            floorY = hit.point.y;
        }
        return floorY;
    }

    // The two model files aren't the same scale (Granny's is much bigger), so size each one to a real
    // person's height. Skipped if the model's measured size makes no sense (bad bounds in the file).
    private static void ScaleToHeight(GameObject model, float height)
    {
        if (height <= 0f) return;
        float current = MeasureBounds(model).size.y;
        if (current < 0.1f || current > 20f) return;
        model.transform.localScale *= height / current;
    }

    private static Bounds MeasureBounds(GameObject model)
    {
        var renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(model.transform.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    // How far the model's pivot is above its feet. A silly result (bad bounds in the model file)
    // falls back to 0, i.e. the pivot is at the feet like most character models.
    private static float MeasurePivotAboveFeet(GameObject model)
    {
        Bounds b = MeasureBounds(model);
        float pivotAboveFeet = model.transform.position.y - b.min.y;
        return pivotAboveFeet > -0.5f && pivotAboveFeet < 1.2f ? pivotAboveFeet : 0f;
    }
}
