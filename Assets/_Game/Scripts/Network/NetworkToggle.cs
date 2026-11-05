using Fusion;
using UnityEngine;

/// <summary>
/// Shares one open/closed value between both players, for Door and Lid.
/// Setup: put this and a NetworkObject on the same pivot object as the Door or Lid.
/// Without a Fusion runner (solo build, Main Menu offline launch), Door and Lid ignore it and work locally.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NetworkToggle : NetworkBehaviour
{
    // The shared value. Fusion copies it to the other player whenever it changes.
    [Networked] public NetworkBool IsOpen { get; private set; }

    private bool startOpen;

    // True once Fusion has connected this object, so the shared value can be used.
    public bool IsReady => Object && Object.IsValid;

    // Door and Lid call this in Awake so the scene's starting state is kept.
    public void SetStartOpen(bool open) => startOpen = open;

    public override void Spawned()
    {
        // Only the owner (the master client for scene objects) writes the first value.
        if (HasStateAuthority) IsOpen = startOpen;
    }

    // Either player can ask; the request goes to the owner, who flips the value for everyone.
    public void RequestToggle() => RpcToggle();

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RpcToggle()
    {
        IsOpen = !IsOpen;
    }
}
