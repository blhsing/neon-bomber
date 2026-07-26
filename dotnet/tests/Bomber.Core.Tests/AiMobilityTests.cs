using Xunit;

namespace Bomber.Core.Tests;

public sealed class AiMobilityTests
{
    [Fact]
    public void AiCanRecenterWhileMovingOutOfAnOverlappedCrateEdge()
    {
        var session = new GameSession(new GameConfiguration
        {
            Seed = 2026,
            TargetCrowns = 9,
            CrateDensity = 0,
            ItemDropChance = 0,
            BombFuseSeconds = 1.90,
            FlameLifetimeSeconds = 0.20,
            SpawnProtectionSeconds = 0,
            Players =
            [
                Computer("Nova", AiDifficulty.Standard),
                new() { Name = "Human", Kind = PlayerKind.Human }
            ]
        });
        session.StartMatch();
        session.DebugSetTile(new GridPosition(4, 3), TileType.Crate);
        session.DebugSetPlayerPosition(0, x: 5.2825, y: 3.5);
        var before = session.Players[0];

        session.Tick(1.0 / 60);

        var after = session.Players[0];
        Assert.True(after.X > before.X, $"Expected the AI to move away from the crate edge; X remained {after.X:F4}.");
        Assert.Equal(3.5, after.Y, precision: 6);

        session.Tick(1.0);
        Assert.True(session.Players[0].Statistics.BombsPlaced > 0, "The AI never centered and resumed clearing the adjacent crate.");
    }

    [Fact]
    public void OverlappingAiPlayersSeparateInsteadOfAcceptingAnEmptyAttackRoute()
    {
        var session = new GameSession(new GameConfiguration
        {
            Seed = 2026,
            TargetCrowns = 9,
            CrateDensity = 0,
            ItemDropChance = 0,
            BombFuseSeconds = 1.90,
            FlameLifetimeSeconds = 0.20,
            SpawnProtectionSeconds = 0,
            Players =
            [
                Computer("Nova", AiDifficulty.Standard),
                Computer("Pulse", AiDifficulty.Standard)
            ]
        });
        session.StartMatch();
        session.DebugSetPlayerPosition(0, new GridPosition(5, 5));
        session.DebugSetPlayerPosition(1, new GridPosition(5, 5));

        session.Tick(0.25);

        var players = session.Players;
        Assert.Contains(players, player => Math.Abs(player.X - 5.5) > 0.01 || Math.Abs(player.Y - 5.5) > 0.01);
        Assert.Contains(players, player => session.DebugGetAiRoute(player.Id).Count > 0 || player.ActiveBombs > 0);
    }

    [Theory]
    [InlineData(73_031)]
    [InlineData(44_221)]
    [InlineData(91_337)]
    public void LivingAiPlayersDoNotRemainStuckOnAStaleRoute(int seed)
    {
        var session = new GameSession(new GameConfiguration
        {
            Seed = seed,
            TargetCrowns = 9,
            CrateDensity = 0.60,
            ItemDropChance = 0,
            BombFuseSeconds = 1.90,
            FlameLifetimeSeconds = 0.20,
            SpawnProtectionSeconds = 0,
            Players =
            [
                new() { Name = "Human", Kind = PlayerKind.Human },
                Computer("Nova", AiDifficulty.Expert),
                Computer("Pulse", AiDifficulty.Standard),
                Computer("Byte", AiDifficulty.Novice)
            ]
        });
        session.StartMatch();
        for (var playerId = 0; playerId < 4; playerId++)
        {
            session.DebugApplyPowerUp(playerId, PowerUpKind.FlamePass);
        }

        var lastPositions = session.Players.ToDictionary(player => player.Id, player => (player.X, player.Y));
        var lastMovementAt = session.Players.ToDictionary(player => player.Id, _ => 0.0);
        const int stepsPerSecond = 60;
        const int simulatedSeconds = 60;
        for (var step = 1; step <= simulatedSeconds * stepsPerSecond; step++)
        {
            session.Tick(1.0 / stepsPerSecond);
            var elapsed = step / (double)stepsPerSecond;
            foreach (var player in session.Players.Where(player => player.Kind == PlayerKind.Computer))
            {
                var previous = lastPositions[player.Id];
                if (Math.Abs(player.X - previous.X) > 1e-6 || Math.Abs(player.Y - previous.Y) > 1e-6)
                {
                    lastPositions[player.Id] = (player.X, player.Y);
                    lastMovementAt[player.Id] = elapsed;
                }

                var stationarySeconds = elapsed - lastMovementAt[player.Id];
                var route = session.DebugGetAiRoute(player.Id);
                var routeDescription = route.Count == 0
                    ? "empty"
                    : string.Join(" -> ", route.Select(cell => $"({cell.X},{cell.Y})={session.Board[cell]}"));
                Assert.True(
                    stationarySeconds < 4,
                    $"AI {player.Id} remained stationary for {stationarySeconds:F2}s at ({player.X:F3}, {player.Y:F3}); intent: {player.AiIntent}, frozen: {player.FrozenSeconds:F2}, bombs: {player.ActiveBombs}/{player.BombCapacity}, route: {routeDescription}.");
            }
        }
    }

    private static PlayerSlotConfiguration Computer(string name, AiDifficulty difficulty) =>
        new() { Name = name, Kind = PlayerKind.Computer, Difficulty = difficulty };
}
