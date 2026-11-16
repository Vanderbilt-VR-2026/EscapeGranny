
using Fusion;
using UnityEngine;

public class PlayerHideState : NetworkBehaviour
{
    public static PlayerHideState LocalPlayer { get; private set; }

    [Networked]
    public NetworkBool IsHidden { get; set; }

    [Header("Avatar Visuals")]
    [SerializeField]
    private Transform visualRoot;

    private Renderer[] avatarRenderers;

    private bool hasLoggedVisibility;
    private bool lastLoggedHidden;

    public override void Spawned()
    {
        if (visualRoot != null)
        {
            avatarRenderers =
                visualRoot.GetComponentsInChildren<Renderer>(true);

            Debug.Log(
                $"[HideState] Found {avatarRenderers.Length} renderers."
            );
        }
        else
        {
            Debug.LogError(
                "[HideState] Assign Visual Root in Inspector."
            );
        }

        // In Shared Mode, the owning client has State Authority.
        if (HasStateAuthority)
        {
            LocalPlayer = this;
            Debug.Log("[HideState] This is my locally owned avatar.");
        }

        UpdateVisibility();
    }

    public override void Render()
    {
        UpdateVisibility();
    }

    public void RequestHide()
    {
        if (!HasStateAuthority)
        {
            Debug.LogError(
                "[HideState] Cannot hide: this client has no State Authority."
            );
            return;
        }

        IsHidden = true;

        Debug.Log("[HideState] Hide requested. IsHidden = true.");
    }

    public void RequestUnhide()
    {
        if (!HasStateAuthority)
        {
            Debug.LogError(
                "[HideState] Cannot unhide: this client has no State Authority."
            );
            return;
        }

        IsHidden = false;

        Debug.Log("[HideState] Unhide requested. IsHidden = false.");
    }

    private void UpdateVisibility()
    {
        if (avatarRenderers == null)
            return;

        // Keep the owner's own avatar visible.
        // Hide it on every other client when IsHidden is true.
        bool shouldBeVisible =
            !IsHidden || HasStateAuthority;

        foreach (Renderer avatarRenderer in avatarRenderers)
        {
            if (avatarRenderer != null)
            {
                avatarRenderer.enabled = shouldBeVisible;
            }
        }

        // Log only when the hidden state changes.
        if (!hasLoggedVisibility || lastLoggedHidden != IsHidden)
        {
            Debug.Log(
                $"[HideState] IsHidden={IsHidden}, " +
                $"HasStateAuthority={HasStateAuthority}, " +
                $"Visible={shouldBeVisible}"
            );

            lastLoggedHidden = IsHidden;
            hasLoggedVisibility = true;
        }
    }

    public override void Despawned(
        NetworkRunner runner,
        bool hasState)
    {
        if (LocalPlayer == this)
        {
            LocalPlayer = null;
        }
    }
}
