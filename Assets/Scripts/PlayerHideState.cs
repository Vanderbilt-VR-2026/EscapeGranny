
using Fusion;
using UnityEngine;

public class PlayerHideState : NetworkBehaviour
{
    // The local player's instance on this client.
    public static PlayerHideState LocalPlayer { get; private set; }

    // This value is synchronized through Photon Fusion.
    [Networked]
    public NetworkBool IsHidden { get; set; }

    [Header("Avatar Visuals")]
    [SerializeField]
    private Transform visualRoot;

    private Renderer[] avatarRenderers;

    public override void Spawned()
    {
        // Cache all renderers belonging to the avatar.
        if (visualRoot != null)
        {
            avatarRenderers =
                visualRoot.GetComponentsInChildren<Renderer>(true);
        }
        else
        {
            Debug.LogError(
                "PlayerHideState: Assign Visual Root in Inspector."
            );
        }

        // Identify this client's own player.
        if (Object.HasInputAuthority)
        {
            LocalPlayer = this;
        }

        UpdateVisibility();
    }

    // Called by Fusion when rendering networked objects.
    public override void Render()
    {
        UpdateVisibility();
    }

    // Called when the player selects the Hide prompt.
    public void RequestHide()
    {
        RPC_SetHidden(true);
    }

    // Called when the player exits the hiding spot.
    public void RequestUnhide()
    {
        RPC_SetHidden(false);
    }

    // The request goes to the State Authority.
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetHidden(bool hidden)
    {
        IsHidden = hidden;
    }

    private void UpdateVisibility()
    {
        if (avatarRenderers == null)
            return;

        // Don't hide your own first-person avatar.
        // Other clients hide this avatar when IsHidden is true.
        bool shouldBeVisible =
            !IsHidden || Object.HasInputAuthority;

        foreach (Renderer avatarRenderer in avatarRenderers)
        {
            if (avatarRenderer != null)
            {
                avatarRenderer.enabled = shouldBeVisible;
            }
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (LocalPlayer == this)
        {
            LocalPlayer = null;
        }
    }
}
