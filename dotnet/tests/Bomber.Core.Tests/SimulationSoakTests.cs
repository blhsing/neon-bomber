using Xunit;

namespace Bomber.Core.Tests;

public sealed class SimulationSoakTests
{
    [Fact]
    public void HumanControlsBufferedTurnsAndThreeAiCanRunForFiveMinutes()
    {
        var session = new GameSession(new GameConfiguration
        {
            Seed = 44_221,
            TargetCrowns = 9,
            CrateDensity = 0.75,
            ItemDropChance = 1,
            BombFuseSeconds = 0.40,
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
            session.DebugApplyPowerUp(playerId, PowerUpKind.BombCapacity);
            session.DebugApplyPowerUp(playerId, PowerUpKind.FireRange);
        }

        var directions = new (int X, int Y)[] { (1, 0), (0, 1), (-1, 0), (0, -1) };
        var kinds = Enum.GetValues<PowerUpKind>();
        const int stepsPerSecond = 60;
        const int simulatedSeconds = 300;
        for (var step = 0; step < simulatedSeconds * stepsPerSecond; step++)
        {
            // Change direction before most lane centers so the buffered corner-turn path is
            // exercised continuously, including turns rejected by walls and bombs.
            var direction = directions[(step / 11) % directions.Length];
            var placeBomb = step % 24 == 0;
            var useAction = step % 137 == 0;
            Assert.True(session.SetControls(0, new(direction.X, direction.Y, placeBomb, useAction)));

            if (step % 19 == 0)
            {
                var human = session.Players[0];
                session.DebugSpawnItem(human.Cell, kinds[(step / 19) % kinds.Length]);
            }

            session.TickInteractive(1.0 / stepsPerSecond);
            var snapshot = session.Snapshot;
            Assert.Equal(GamePhase.Playing, snapshot.Phase);
            Assert.All(snapshot.Bombs, bomb =>
            {
                Assert.True(double.IsFinite(bomb.FuseSeconds));
                Assert.NotEmpty(bomb.FlamePreviewCells);
            });
        }

        var humanAtEnd = session.Players[0];
        Assert.True(humanAtEnd.IsAlive);
        Assert.True(humanAtEnd.Statistics.BombsPlaced > 10);
        Assert.True(humanAtEnd.Statistics.ItemsCollected > 100);
    }

    [Theory]
    [InlineData(73_031)]
    [InlineData(2026)]
    [InlineData(91_337)]
    public void FourAiPlayersSurviveExtendedBombItemAndSnapshotStress(int seed)
    {
        var session = new GameSession(new GameConfiguration
        {
            Seed = seed,
            TargetCrowns = 9,
            CrateDensity = 0.90,
            ItemDropChance = 1,
            BombFuseSeconds = 0.25,
            FlameLifetimeSeconds = 0.20,
            SpawnProtectionSeconds = 0,
            Players =
            [
                Computer("Nova", AiDifficulty.Expert),
                Computer("Pulse", AiDifficulty.Expert),
                Computer("Byte", AiDifficulty.Standard),
                Computer("Echo", AiDifficulty.Novice)
            ]
        });
        session.StartMatch();

        // Keep the arena active for the whole soak while explosions, chain reactions,
        // item collection, AI planning, and snapshot projection run at high frequency.
        for (var playerId = 0; playerId < 4; playerId++)
        {
            session.DebugApplyPowerUp(playerId, PowerUpKind.FlamePass);
            session.DebugApplyPowerUp(playerId, PowerUpKind.BombCapacity);
            session.DebugApplyPowerUp(playerId, PowerUpKind.BombCapacity);
            session.DebugApplyPowerUp(playerId, PowerUpKind.FireRange);
        }

        var kinds = Enum.GetValues<PowerUpKind>();
        var maximumBombs = 0;
        var maximumItems = 0;
        const int simulatedSeconds = 120;
        const int stepsPerSecond = 60;
        for (var step = 0; step < simulatedSeconds * stepsPerSecond; step++)
        {
            if (step % 12 == 0)
            {
                var playerId = (step / 12) % 4;
                var player = session.Players[playerId];
                session.DebugSpawnItem(player.Cell, kinds[(step / 12) % kinds.Length]);
            }

            if (step % 30 == 0)
            {
                var playerId = (step / 30) % 4;
                session.DebugApplyPowerUp(playerId, PowerUpKind.Cluster);
                session.DebugApplyPowerUp(playerId, PowerUpKind.Mega);
            }

            session.Tick(1.0 / stepsPerSecond);
            var snapshot = session.Snapshot;
            maximumBombs = Math.Max(maximumBombs, snapshot.Bombs.Count);
            maximumItems = Math.Max(maximumItems, snapshot.Items.Count);

            Assert.Equal(GamePhase.Playing, snapshot.Phase);
            Assert.All(snapshot.Players, player =>
            {
                Assert.True(double.IsFinite(player.X));
                Assert.True(double.IsFinite(player.Y));
            });
            Assert.All(snapshot.Bombs, bomb => Assert.NotEmpty(bomb.FlamePreviewCells));
        }

        Assert.True(maximumBombs > 0, "The stress match never exercised live bombs.");
        Assert.True(maximumItems > 0, "The stress match never exercised live items.");
        Assert.True(session.Players.Sum(player => player.Statistics.ItemsCollected) > 100);
    }

    private static PlayerSlotConfiguration Computer(string name, AiDifficulty difficulty) =>
        new() { Name = name, Kind = PlayerKind.Computer, Difficulty = difficulty };
}
