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
