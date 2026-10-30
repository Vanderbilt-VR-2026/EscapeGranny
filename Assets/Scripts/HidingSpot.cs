using Fusion;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables; // XRI 3.x. Remove this line on XRI 2.x.

// PUT THIS ON EACH BED/LOCKER (needs a NetworkObject, placed in the scene).
public class HidingSpot : NetworkBehaviour
{
    [Networked] public NetworkBool Occupied { get; set; }

    [SerializeField] Transform hidePoint, exitPoint;
    [SerializeField] XRSimpleInteractable interactable;

    public static LocalPlayerRig LocalRig;

    void Awake()     => interactable.selectEntered.AddListener(OnSelected);
    void OnDestroy() => interactable.selectEntered.RemoveListener(OnSelected);

    void OnSelected(SelectEnterEventArgs args)
    {
        if (Object == null || !Object.IsValid)
        {
            Debug.LogWarning("[Hide] Bed isn't spawned by Fusion, skipping the network request (offline test).");
            return;
        }
        RPC_RequestHide();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    void RPC_RequestHide(RpcInfo info = default)
    {
        Debug.Log($"[Hide] RequestHide received from {info.Source}, Occupied={Occupied}");
        if (Occupied) return;
        var playerObj = Runner.GetPlayerObject(info.Source);
        if (playerObj == null) return; // spawner must call runner.SetPlayerObject(...)

        Occupied = true;
        playerObj.GetComponent<PlayerHideState>().IsHidden = true;
        RPC_Enter(info.Source);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestExit(RpcInfo info = default)
    {
        var playerObj = Runner.GetPlayerObject(info.Source);
        if (playerObj != null)
            playerObj.GetComponent<PlayerHideState>().IsHidden = false;
        Occupied = false;
        RPC_Exit(info.Source);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_Enter(PlayerRef who)
    {
        Debug.Log($"[Hide] Enter for {who}, local={Runner.LocalPlayer}, LocalRig null? {LocalRig == null}");
        if (who == Runner.LocalPlayer && LocalRig != null) LocalRig.EnterHiding(hidePoint, this);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    void RPC_Exit(PlayerRef who)
    {
        if (who == Runner.LocalPlayer && LocalRig != null) LocalRig.ExitHiding(exitPoint);
    }
}
