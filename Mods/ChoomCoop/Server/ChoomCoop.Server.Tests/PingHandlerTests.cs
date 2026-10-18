using ChoomCoop.Shared.Protocol;
using ChoomCoopSystem.Core;
using Xunit;

namespace ChoomCoop.Server.Tests
{
    public class PingHandlerTests
    {
        [Fact]
        public void MatchingVersion_IsAcceptedAndEchoesNonce()
        {
            var reply = PingHandler.Handle(ProtocolVersion.Current, 4242);

            Assert.True(reply.Accepted);
            Assert.Equal(4242u, reply.Nonce);
            Assert.Equal(ProtocolVersion.Current, reply.ProtocolVersion);
        }

        [Fact]
        public void MismatchedVersion_IsRejectedButStillReportsServerVersion()
        {
            var reply = PingHandler.Handle(ProtocolVersion.Current + 1, 7);

            Assert.False(reply.Accepted);
            Assert.Equal(7u, reply.Nonce);
            Assert.Equal(ProtocolVersion.Current, reply.ProtocolVersion);
        }

        [Fact]
        public void ModVersion_IsPhase0()
        {
            Assert.Equal("0.0.1", ModInfo.Version);
        }
    }
}
