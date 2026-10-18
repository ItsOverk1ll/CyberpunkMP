// Hand-written in the exact shape CyberpunkMP's generator (client --rpc) emits.
// Ids come from ChoomCoop.Shared.Protocol.RpcIds and are verified by RpcIdTests.

#pragma warning disable CS8618
#pragma warning disable CS0219

using ChoomCoop.Shared.Protocol;
using CyberpunkSdk;
using CyberpunkSdk.Types;
using CyberpunkSdk.Systems;
using CyberpunkSdk.Internal;

namespace Cyberpunk.Rpc.Client.Protocol
{
public static class CoopClient
{
	[ClientRpc(Class = RpcIds.CoopClientClassId, Function = RpcIds.PongId)]
	public static void Pong(ulong playerId, uint protocolVersion, uint nonce, bool accepted)
	{
		var data = IBuffer.Create();
		try{
			data.WriteUint32(protocolVersion);
			data.WriteUint32(nonce);
			data.WriteBool(accepted);
			CyberpunkSdk.Server.RpcManager.Call(playerId, RpcIds.CoopClientClassId, RpcIds.PongId, data);
		} finally {
			IBuffer.Release(data);
		}
	}
}
}
