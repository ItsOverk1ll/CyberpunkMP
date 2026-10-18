using System.Text.RegularExpressions;
using ChoomCoop.Shared.Protocol;
using Xunit;

namespace ChoomCoop.Server.Tests
{
    public class RpcIdTests
    {
        [Fact]
        public void Fnv1a64_MatchesUpstreamGeneratedEmoteIds()
        {
            // Values copied from upstream's generated code/server/scripting/EmoteSystem stubs.
            Assert.Equal(16273172002596938280UL, RpcIds.Fnv1a64("CyberpunkMP.Plugins.EmoteServer"));
            Assert.Equal(8330132648100619273UL, RpcIds.Fnv1a64("TriggerEmote"));
            Assert.Equal(17492243237040389260UL, RpcIds.Fnv1a64("CyberpunkMP.Plugins.EmoteClient"));
            Assert.Equal(5701494315076382889UL, RpcIds.Fnv1a64("TriggerEmote;Uint64String"));
        }

        [Fact]
        public void ChoomCoopIds_MatchTheirNames()
        {
            Assert.Equal(RpcIds.Fnv1a64(RpcIds.CoopServerClass), RpcIds.CoopServerClassId);
            Assert.Equal(RpcIds.Fnv1a64(RpcIds.PingFunction), RpcIds.PingId);
            Assert.Equal(RpcIds.Fnv1a64(RpcIds.CoopClientClass), RpcIds.CoopClientClassId);
            Assert.Equal(RpcIds.Fnv1a64(RpcIds.PongFunction), RpcIds.PongId);
        }

        [Fact]
        public void RedscriptProtocolFile_MatchesCSharpVersionAndNames()
        {
            var reds = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Shared", "Protocol", "Protocol.reds"));

            var version = Regex.Match(reds, @"func Version\(\) -> Uint32 \{\s*return (\d+)u;");
            Assert.True(version.Success, "ChoomCoopProtocol.Version() not found in Protocol.reds");
            Assert.Equal(ProtocolVersion.Current, uint.Parse(version.Groups[1].Value));

            Assert.Contains("module ChoomCoop.Protocol", reds);
            Assert.Contains("class CoopServer extends ServerRpc", reds);
            Assert.Contains("class CoopClient extends ClientRpc", reds);
            Assert.Contains("func Ping(protocolVersion: Uint32, nonce: Uint32)", reds);
            Assert.Contains("func Pong(protocolVersion: Uint32, nonce: Uint32, accepted: Bool)", reds);
        }
    }
}
