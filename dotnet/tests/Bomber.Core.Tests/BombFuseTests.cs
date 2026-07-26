using Xunit;

namespace Bomber.Core.Tests;

public sealed class BombFuseTests
{
    [Fact]
    public void NormalBombUsesTheBalancedOnePointNineSecondFuse()
    {
        var session = TestSessionFactory.Create();

        Assert.Equal(1.90, session.Configuration.BombFuseSeconds);
        TapBomb(session, 0);
        var normalBomb = Assert.Single(session.Bombs);
        Assert.Equal(1.90, normalBomb.InitialFuseSeconds);
        Assert.InRange(normalBomb.FuseSeconds, 1.85, 1.90);

        session.DebugSetPlayerPosition(0, new(3, 3));
        session.Tick(1.78);
        Assert.Single(session.Bombs);

        session.Tick(0.14);
        Assert.Empty(session.Bombs);
        Assert.NotEmpty(session.Flames);
    }

    [Fact]
    public void RemoteBombKeepsItsLongFuseUntilTheOwnerTriggersIt()
    {
        var session = TestSessionFactory.Create();
        session.DebugApplyPowerUp(0, PowerUpKind.Remote);

        TapBomb(session, 0);
        var remoteBomb = Assert.Single(session.Bombs);
        Assert.Equal(8, remoteBomb.InitialFuseSeconds);
        Assert.InRange(remoteBomb.FuseSeconds, 7.95, 8.00);

        session.DebugSetPlayerPosition(0, new(3, 3));
        session.Tick(1.60);
        Assert.Single(session.Bombs);

        Assert.True(session.SetControls(0, new PlayerControls(0, 0, UseAction: true)));
        session.Tick(0.02);

        Assert.Empty(session.Bombs);
        Assert.NotEmpty(session.Flames);
    }

    private static void TapBomb(GameSession session, int playerId)
    {
        Assert.True(session.SetControls(playerId, new PlayerControls(0, 0, PlaceBomb: true)));
        session.Tick(0.02);
        Assert.True(session.SetControls(playerId, PlayerControls.None));
    }
}
