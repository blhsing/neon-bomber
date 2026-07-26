using Xunit;

namespace Bomber.Core.Tests;

public sealed class InteractivePerformanceTests
{
    [Fact]
    public void InteractiveTickDiscardsAUiThreadStallInsteadOfCatchingUpBombTime()
    {
        var session = TestSessionFactory.Create();
        Assert.True(session.SetControls(0, new PlayerControls(0, 0, PlaceBomb: true)));
        session.Tick(0.02);
        session.SetControls(0, PlayerControls.None);
        session.DebugSetPlayerPosition(0, new(3, 3));
        var before = Assert.Single(session.Bombs).FuseSeconds;

        session.TickInteractive(5.0);

        var after = Assert.Single(session.Bombs).FuseSeconds;
        Assert.InRange(before - after, 0.015, GameSession.MaximumInteractiveFrameSeconds + 0.001);
    }

    [Fact]
    public void BoardSnapshotIsReusedUntilATileActuallyChanges()
    {
        var session = TestSessionFactory.Create();
        var initial = session.Board;

        session.Tick(0.10);

        Assert.Same(initial, session.Board);
        session.DebugSetTile(new(3, 3), TileType.Crate);
        var changed = session.Board;
        Assert.NotSame(initial, changed);
        Assert.Equal(TileType.Crate, changed[3, 3]);
    }

    [Fact]
    public void TrappedAiHonorsItsThinkCooldownInsteadOfPathfindingEveryStep()
    {
        var session = new GameSession(new GameConfiguration
        {
            Seed = 19,
            CrateDensity = 0,
            SpawnProtectionSeconds = 0,
            Players =
            [
                new() { Name = "Human", Kind = PlayerKind.Human },
                new() { Name = "Trapped AI", Kind = PlayerKind.Computer, Difficulty = AiDifficulty.Standard }
            ]
        });
        session.StartMatch();
        session.DebugSetTile(new(14, 11), TileType.SolidWall);
        session.DebugSetTile(new(15, 10), TileType.SolidWall);

        session.Tick(0.02);
        var afterPlanning = session.DebugGetAiThinkRemaining(1);
        session.Tick(0.02);
        var afterCooldownStep = session.DebugGetAiThinkRemaining(1);

        Assert.InRange(afterPlanning, 0.20, 0.22);
        Assert.True(afterCooldownStep < afterPlanning - 0.01,
            $"Expected the AI cooldown to count down, but it changed from {afterPlanning} to {afterCooldownStep}.");
    }
}
