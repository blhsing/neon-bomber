using Xunit;

namespace Bomber.Core.Tests;

public sealed class RoundAndLifecycleTests
{
    [Fact]
    public void ArenaAndRoundStartHaveExpectedBasics()
    {
        var session = TestSessionFactory.Create(playerCount: 4);

        Assert.Equal(GamePhase.Playing, session.Phase);
        Assert.Equal(1, session.RoundNumber);
        Assert.Equal(17, session.Board.Width);
        Assert.Equal(13, session.Board.Height);
        Assert.Equal(TileType.SolidWall, session.Board[0, 0]);
        Assert.Equal(TileType.SolidWall, session.Board[2, 2]);
        Assert.All(ArenaRules.SpawnCells, spawn => Assert.Equal(TileType.Floor, session.Board[spawn]));
        Assert.Equal(4, session.Players.Count);
        Assert.All(session.Players, player => Assert.True(player.IsAlive));
    }

    [Fact]
    public void CrownsPersistAcrossRoundsAndTargetEndsMatch()
    {
        var session = TestSessionFactory.Create(targetCrowns: 2);

        session.DebugEliminatePlayer(1, sourcePlayerId: 0);
        session.Tick(0.02);

        Assert.Equal(GamePhase.RoundOver, session.Phase);
        Assert.Equal(0, session.LastRound?.WinnerPlayerId);
        Assert.Equal(1, session.Players[0].Crowns);
        Assert.Equal(1, session.Players[0].Statistics.RoundsWon);

        session.StartNextRound();

        Assert.Equal(2, session.RoundNumber);
        Assert.Null(session.LastRound);
        Assert.All(session.Players, player => Assert.True(player.IsAlive));
        Assert.Equal(1, session.Players[0].Crowns);

        session.DebugEliminatePlayer(1, sourcePlayerId: 0);
        session.Tick(0.02);

        Assert.Equal(GamePhase.MatchOver, session.Phase);
        Assert.Equal(0, session.MatchWinnerPlayerId);
        Assert.Equal(2, session.Players[0].Crowns);
        Assert.Throws<InvalidOperationException>(session.StartNextRound);
    }

    [Fact]
    public void DrawAwardsNoCrown()
    {
        var session = TestSessionFactory.Create();
        session.DebugEliminatePlayer(0);
        session.DebugEliminatePlayer(1);

        session.Tick(0.02);

        Assert.Equal(GamePhase.RoundOver, session.Phase);
        Assert.True(session.LastRound?.IsDraw);
        Assert.Null(session.LastRound?.WinnerPlayerId);
        Assert.All(session.Players, player => Assert.Equal(0, player.Crowns));
    }

    [Fact]
    public void LifecyclePauseFreezesAndThenResumesSimulation()
    {
        var session = TestSessionFactory.Create(fuseSeconds: 1);
        session.DebugPlaceBomb(0, new(3, 3), fuse: 1, range: 1);
        var initialFuse = Assert.Single(session.Bombs).FuseSeconds;

        session.HandleLifecycle(SessionLifecycleEvent.Backgrounded);
        session.Tick(0.5);

        Assert.Equal(GamePhase.Paused, session.Phase);
        Assert.Equal(initialFuse, Assert.Single(session.Bombs).FuseSeconds);

        session.HandleLifecycle(SessionLifecycleEvent.Foregrounded);
        session.Tick(0.5);

        Assert.Equal(GamePhase.Playing, session.Phase);
        Assert.True(Assert.Single(session.Bombs).FuseSeconds < initialFuse - 0.45);
    }
}
