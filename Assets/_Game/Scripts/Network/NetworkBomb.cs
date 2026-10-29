using Fusion;
using UnityEngine;

/// <summary>
/// Lets the other player see the bomb's pin come out, the explosion, and the bomb coming back.
/// The throw itself is synced by XRGrabNetworkInteractable + NetworkTransform. No damage yet.
/// Setup: put this on Bomb_Root next to Bomb and XRGrabNetworkInteractable.
/// Without a Fusion runner, Bomb ignores it and works locally as before.
/// </summary>
[RequireComponent(typeof(NetworkObject), typeof(Bomb))]
public class NetworkBomb : NetworkBehaviour
{
    private Bomb bomb;

    // True once Fusion has connected this object, so RPCs can be sent.
    public bool IsReady => Object && Object.IsValid;

    private void Awake() => bomb = GetComponent<Bomb>();

    public void RequestPullPin() => RpcPullPin();
    public void RequestExplode(Vector3 position) => RpcExplode(position);
    public void RequestReset() => RpcReset();

    // Jump straight to the hand on both headsets instead of sliding across the room.
    public void Teleport(Vector3 position, Quaternion rotation)
    {
        if (HasStateAuthority && TryGetComponent<NetworkTransform>(out var networkTransform))
            networkTransform.Teleport(position, rotation);
        transform.SetPositionAndRotation(position, rotation);
    }

    // All three run on both headsets, including the one that sent them.
    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RpcPullPin() => bomb.ApplyPinPulled();

    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RpcExplode(Vector3 position) => bomb.ApplyExplosion(position);

    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RpcReset() => bomb.ApplyReset();
}
