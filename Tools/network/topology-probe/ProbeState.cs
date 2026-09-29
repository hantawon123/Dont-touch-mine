using Fusion;
using UnityEngine;

// Copied into an isolated project by prepare.py; never compiled into the game.
public sealed class ProbeState : NetworkBehaviour
{
    [Networked] public int AuthorityTicks { get; set; }
    [Networked] public int Requests { get; set; }
    [Networked] public int Players { get; set; }
    [Networked] public float Height { get; set; }

    public override void Spawned()
    {
        GetComponent<Rigidbody>().isKinematic = !HasStateAuthority;
        if (!HasStateAuthority) RPC_Request();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        AuthorityTicks++;
        Height = transform.position.y;
        var count = 0;
        foreach (var _ in Runner.ActivePlayers) count++;
        Players = count;
    }

    public override void Render()
    {
        if (!HasStateAuthority) transform.position = new Vector3(0, Height, 0);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_Request(RpcInfo info = default)
    {
        if (!info.Source.IsRealPlayer) return;
        Requests++;
        Debug.Log("[Probe] authority accepted request from " + info.Source);
    }
}
