using Xunit;

namespace Bomber.Core.Tests;

public sealed class PowerUpCatalogTests
{
    [Fact]
    public void CatalogRetainsAllOriginalIdsAndExactPriorityWeights()
    {
        var expectedIds = new[]
        {
            "bomb", "fire", "speed", "kick", "glove", "remote", "disguise", "pierce", "bombpass",
            "wallpass", "flamepass", "shield", "heart", "dash", "mega", "cluster", "freeze", "magnet", "mystery"
        };

        Assert.Equal(expectedIds, PowerUpCatalog.All.Select(power => power.Id));
        Assert.Equal(19, PowerUpCatalog.All.Select(power => power.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(30, PowerUpCatalog.Get("bomb").Weight);
        Assert.Equal(30, PowerUpCatalog.Get("fire").Weight);
        Assert.All(
            PowerUpCatalog.All.Where(power => power.Id is not "bomb" and not "fire"),
            power => Assert.InRange(power.Weight, 1, 10));
        Assert.Equal(143, PowerUpCatalog.TotalWeight);
    }

    [Fact]
    public void CapacityAndRangeTogetherOwnSixtyOfOneHundredFortyThreeTickets()
    {
        var priorityWeight = PowerUpCatalog.Get(PowerUpKind.BombCapacity).Weight +
                             PowerUpCatalog.Get(PowerUpKind.FireRange).Weight;
        var probability = (double)priorityWeight / PowerUpCatalog.TotalWeight;

        Assert.Equal(60, priorityWeight);
        Assert.Equal(60.0 / 143.0, probability, precision: 12);
        Assert.True(probability > 0.41);
        Assert.All(
            PowerUpCatalog.All.Where(power => power.Kind is not PowerUpKind.BombCapacity and not PowerUpKind.FireRange),
            power => Assert.True(PowerUpCatalog.Get(PowerUpKind.BombCapacity).Weight >= power.Weight * 3));
    }

    [Fact]
    public void SeededWeightedSelectionHasAStableSequence()
    {
        var random = new DeterministicRandom(2026);
        var actual = Enumerable.Range(0, 16)
            .Select(_ => PowerUpCatalog.Select(random).Id)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "bomb", "bomb", "speed", "dash", "shield", "cluster", "fire", "freeze",
                "kick", "bomb", "dash", "mystery", "heart", "fire", "fire", "mega"
            },
            actual);
    }
}
