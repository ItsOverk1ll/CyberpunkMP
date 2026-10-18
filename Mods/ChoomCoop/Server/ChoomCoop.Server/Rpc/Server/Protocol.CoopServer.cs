// Hand-written in the exact shape CyberpunkMP's generator (client --rpc) emits.
// Ids come from ChoomCoop.Shared.Protocol.RpcIds and are verified by RpcIdTests.

#pragma warning disable CS8618
#pragma warning disable CS0219

using ChoomCoop.Shared.Protocol;
using CyberpunkSdk;
using CyberpunkSdk.Types;
using CyberpunkSdk.Systems;
using CyberpunkSdk.Internal;

namespace Cyberpunk.Rpc.Server.Protocol
{
public static partial class CoopServer
{
	[ServerRpc(Class = RpcIds.CoopServerClassId, Function = RpcIds.PingId)]
	public static void Ping(ulong playerId, IBuffer data)
	{
		var protocolVersion = data.ReadUint32();
		var nonce = data.ReadUint32();

		Ping_Impl(playerId, protocolVersion, nonce);
	}

	public static partial void Ping_Impl(ulong playerId, uint protocolVersion, uint nonce);
}
}
