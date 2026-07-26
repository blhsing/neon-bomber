using Xunit;

namespace Bomber.Core.Tests;

public sealed class AiMovementTests
{
    [Fact]
    public void AiCentersItselfInCrateGoalCellAndPlantsBomb()
    {
        var session = CreateSessionWithComputerPlayer();
        session.DebugClearCrates();
        session.DebugSetTile(new(15, 9), TileType.Crate);

        session.Tick(1.0);

        var computer = session.Players[1];
        Assert.Equal(1, computer.Statistics.BombsPlaced);
        Assert.Contains(session.Bombs, bomb => bomb.OwnerPlayerId == computer.Id);
    }

    [Fact]
    public void ExpertAiDoesNotAbandonBombEscapeForTemporarilySafeDeadEnd()
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
                new() { Name = "Expert", Kind = PlayerKind.Computer, Difficulty = AiDifficulty.Expert },
                new() { Name = "Human", Kind = PlayerKind.Human }
            ]
        });
        session.StartMatch();
        session.DebugSetPlayerPosition(0, new GridPosition(5, 5));
        session.DebugSetPlayerPosition(1, new GridPosition(15, 11));
        session.DebugSetInvulnerability(1, 30);
        session.DebugSetTile(new GridPosition(5, 4), TileType.Crate);
        session.DebugSetTile(new GridPosition(7, 5), TileType.SolidWall);
        session.DebugApplyPowerUp(0, PowerUpKind.FireRange);

        for (var step = 0; step < 60 && session.Bombs.Count == 0; step++)
        {
            session.Tick(1.0 / 60);
        }

        Assert.Single(session.Bombs, bomb => bomb.OwnerPlayerId == 0);
        for (var step = 0; step < 150 && session.Phase == GamePhase.Playing; step++)
        {
            session.Tick(1.0 / 60);
        }

        Assert.True(session.Players[0].IsAlive, "The expert AI oscillated inside its own blast and died.");
    }

    [Fact]
    public void ExpertAiCompletesThreeCellEscapeBeforeBombFuse()
    {
        var session = new GameSession(new GameConfiguration
        {
            Seed = 2026,
            TargetCrowns = 9,
            CrateDensity = 0,
            ItemDropChance = 0,
            BombFuseSeconds = 1.25,
            FlameLifetimeSeconds = 0.20,
            SpawnProtectionSeconds = 0,
            Players =
            [
                new() { Name = "Expert", Kind = PlayerKind.Computer, Difficulty = AiDifficulty.Expert },
                new() { Name = "Observer", Kind = PlayerKind.Human }
            ]
        });
        session.StartMatch();
        session.DebugClearCrates();
        session.DebugSetPlayerPosition(0, new GridPosition(5, 5));
        session.DebugSetPlayerPosition(1, new GridPosition(15, 11));
        session.DebugSetInvulnerability(1, 30);
        session.DebugPlaceBomb(0, new GridPosition(5, 5), fuse: 1.25, range: 2);

        session.Tick(1.0 / 60);

        Assert.True(session.DebugGetAiRoute(0).Count >= 3, "The fixture must require a three-cell escape.");
        for (var step = 0; step < 90 && session.Phase == GamePhase.Playing; step++)
        {
            session.Tick(1.0 / 60);
        }

        Assert.True(session.Players[0].IsAlive, "The expert AI did not traverse its planned route before the fuse expired.");
    }

    [Fact]
    public void ExpertAiDoesNotKillItselfWhileClearingSeededArenas()
    {
        for (var seed = 1; seed <= 10; seed++)
        {
            var session = new GameSession(new GameConfiguration
            {
                Seed = seed,
                TargetCrowns = 9,
                CrateDensity = 0.72,
                ItemDropChance = 0,
                BombFuseSeconds = 1.90,
                FlameLifetimeSeconds = 0.20,
                SpawnProtectionSeconds = 0,
                Players =
                [
                    new() { Name = "Expert", Kind = PlayerKind.Computer, Difficulty = AiDifficulty.Expert },
                    new() { Name = "Observer", Kind = PlayerKind.Human }
                ]
            });
            session.StartMatch();
            session.DebugSetInvulnerability(1, 30);

            for (var step = 0; step < 12 * 60 && session.Phase == GamePhase.Playing; step++)
            {
                session.Tick(1.0 / 60);
            }

            Assert.True(session.Players[0].IsAlive, $"Expert AI killed itself with seed {seed}.");
        }
    }

    [Fact]
    public void ExpertAiRejectsEscapeThatAProposedBombWouldChainExplodeTooSoon()
    {
        var session = new GameSession(new GameConfiguration
        {
            Seed = 2026,
            TargetCrowns = 9,
            CrateDensity = 0,
            ItemDropChance = 0,
            BombFuseSeconds = 1.90,
            SpawnProtectionSeconds = 0,
            Players =
            [
                new() { Name = "Expert", Kind = PlayerKind.Computer, Difficulty = AiDifficulty.Expert },
                new() { Name = "Observer", Kind = PlayerKind.Human }
            ]
        });
        session.StartMatch();
        session.DebugClearCrates();
        session.DebugSetPlayerPosition(0, new GridPosition(5, 5));
        session.DebugSetPlayerPosition(1, new GridPosition(15, 11));
        session.DebugSetInvulnerability(1, 30);

        // The existing bomb blocks the left exit. The crate and walls make the only nominal
        // escape a long corridor to the opening at (11,4).
        session.DebugPlaceBomb(1, new GridPosition(4, 5), fuse: 10, range: 10);
        session.DebugSetTile(new GridPosition(5, 4), TileType.Crate);
        for (var x = 3; x <= 10; x++)
        {
            if (x != 5)
            {
                session.DebugSetTile(new GridPosition(x, 4), TileType.SolidWall);
            }

            session.DebugSetTile(new GridPosition(x, 6), TileType.SolidWall);
        }

        session.Tick(1.0 / 60);

        Assert.Equal(0, session.Players[0].Statistics.BombsPlaced);
        Assert.DoesNotContain(session.Bombs, bomb => bomb.OwnerPlayerId == 0);
    }

    [Fact]
    public void ExpertAiForecastsAnExistingBombChainAtTheEarliestFuse()
    {
        var session = new GameSession(new GameConfiguration
        {
            Seed = 2026,
            TargetCrowns = 9,
            CrateDensity = 0,
            ItemDropChance = 0,
            SpawnProtectionSeconds = 0,
            Players =
            [
                new() { Name = "Expert", Kind = PlayerKind.Computer, Difficulty = AiDifficulty.Expert },
                new() { Name = "Observer", Kind = PlayerKind.Human }
            ]
        });
        session.StartMatch();
        session.DebugClearCrates();
        session.DebugSetPlayerPosition(0, new GridPosition(7, 5));
        session.DebugSetPlayerPosition(1, new GridPosition(15, 11));
        session.DebugSetInvulnerability(1, 30);
        session.DebugPlaceBomb(1, new GridPosition(3, 5), fuse: 0.75, range: 2);
        session.DebugPlaceBomb(1, new GridPosition(5, 5), fuse: 10, range: 2);

        session.Tick(1.0 / 60);

        Assert.Equal("Escaping blast", session.Players[0].AiIntent);
        Assert.NotEmpty(session.DebugGetAiRoute(0));
    }

    private static GameSession CreateSessionWithComputerPlayer()
    {
        var session = new GameSession(new GameConfiguration
        {
            Seed = 2026,
            CrateDensity = 0,
            ItemDropChance = 0,
            BombFuseSeconds = 10,
            SpawnProtectionSeconds = 0,
            Players =
            [
                new() { Name = "Human", Kind = PlayerKind.Human },
                new() { Name = "Computer", Kind = PlayerKind.Computer, Difficulty = AiDifficulty.Standard }
            ]
        });
        session.StartMatch();
        return session;
    }
}
