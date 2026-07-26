using Xunit;

namespace Bomber.Core.Tests;

public sealed class BombCapacityTests
{
    [Fact]
    public void CapacityChipRaisesConcurrentBombLimitAndStopsAtCap()
    {
        var session = TestSessionFactory.Create(fuseSeconds: 10);
        for (var count = 0; count < 20; count++)
        {
            session.DebugApplyPowerUp(0, PowerUpKind.BombCapacity);
        }

        Assert.Equal(PlayerCaps.BombCapacity, session.Players[0].BombCapacity);
        for (var x = 1; x <= PlayerCaps.BombCapacity; x++)
        {
            session.DebugSetPlayerPosition(0, new(x, 1));
            TapBomb(session, 0);
        }

        Assert.Equal(PlayerCaps.BombCapacity, session.Bombs.Count);
        Assert.Equal(PlayerCaps.BombCapacity, session.Players[0].ActiveBombs);
        Assert.Equal(0, session.Players[0].BombsAvailable);

        session.DebugSetPlayerPosition(0, new(10, 1));
        TapBomb(session, 0);

        Assert.Equal(PlayerCaps.BombCapacity, session.Bombs.Count);
        Assert.Equal(PlayerCaps.BombCapacity, session.Players[0].Statistics.BombsPlaced);
    }

    [Fact]
    public void DefaultCapacityAllowsOnlyOneLiveBomb()
    {
        var session = TestSessionFactory.Create(fuseSeconds: 10);
        TapBomb(session, 0);
        session.DebugSetPlayerPosition(0, new(2, 1));
        TapBomb(session, 0);

        Assert.Single(session.Bombs);
        Assert.Equal(1, session.Players[0].ActiveBombs);
    }

    [Fact]
    public void BombLetsEveryOverlappingPlayerLeaveButBlocksReentryAfterTheyClearIt()
    {
        var session = TestSessionFactory.Create(fuseSeconds: 10);
        session.DebugSetPlayerPosition(0, new(3, 3));
        session.DebugSetPlayerPosition(1, new(3, 3));
        TapBomb(session, 0);
        var bombId = Assert.Single(session.Bombs).Id;

        Assert.Equal(new[] { 0, 1 }, session.DebugBombPassThroughPlayers(bombId).Order());

        session.SetControls(0, new(-1, 0));
        session.SetControls(1, new(1, 0));
        session.Tick(0.50);

        Assert.True(session.Players[0].X < 2.70);
        Assert.True(session.Players[1].X > 4.30);
        Assert.Empty(session.DebugBombPassThroughPlayers(bombId));

        session.SetControls(0, new(1, 0));
        session.SetControls(1, new(-1, 0));
        session.Tick(0.50);

        Assert.True(session.Players[0].X <= 2.71);
        Assert.True(session.Players[1].X >= 4.29);
    }

    private static void TapBomb(GameSession session, int playerId)
    {
        Assert.True(session.SetControls(playerId, new(0, 0, PlaceBomb: true)));
        session.Tick(0.02);
        Assert.True(session.SetControls(playerId, PlayerControls.None));
    }
}
