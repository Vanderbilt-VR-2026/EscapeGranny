using Fusion;
using UnityEngine;

/// <summary>
/// Lets the other player see the gun fire (bullet + muzzle flash). No damage yet.
/// Setup: put this on Pistol_Root next to Gun and XRGrabNetworkInteractable.
/// Without a Fusion runner, Gun ignores it and fires locally as before.
/// </summary>
[RequireComponent(typeof(NetworkObject), typeof(Gun))]
public class NetworkGun : NetworkBehaviour
{
    private Gun gun;

    // True once Fusion has connected this object, so RPCs can be sent.
    public bool IsReady => Object && Object.IsValid;

    private void Awake() => gun = GetComponent<Gun>();

    public void RequestFire() => RpcFire();

    // Runs on both headsets (the shooter too). Each one spawns its own bullet and flash,
    // which is fine while bullets are only visual.
    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RpcFire() => gun.Fire();
}
