namespace Bomber.Core.Tests;

internal static class TestSessionFactory
{
    public static GameSession Create(
        int seed = 2026,
        int targetCrowns = 3,
        double itemDropChance = 0,
        double fuseSeconds = 1.90,
        int playerCount = 2)
    {
        var slots = Enumerable.Range(0, playerCount)
            .Select(index => new PlayerSlotConfiguration
            {
                Name = $"Test {index + 1}",
                Kind = PlayerKind.Human
            })
            .ToArray();
        var session = new GameSession(new GameConfiguration
        {
            Seed = seed,
            TargetCrowns = targetCrowns,
            CrateDensity = 0,
            ItemDropChance = itemDropChance,
            BombFuseSeconds = fuseSeconds,
            FlameLifetimeSeconds = 0.60,
            SpawnProtectionSeconds = 0,
            Players = slots
        });
        session.StartMatch();
        return session;
    }
}
