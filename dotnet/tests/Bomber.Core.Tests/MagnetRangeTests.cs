using Xunit;

namespace Bomber.Core.Tests;

public sealed class MagnetRangeTests
{
    [Fact]
    public void MagnetOnlyPullsItemsWithinTwoAndAHalfCells()
    {
        var session = TestSessionFactory.Create(fuseSeconds: 10);
        session.DebugSetPlayerPosition(0, x: 3.1, y: 3.5);
        session.DebugApplyPowerUp(0, PowerUpKind.Magnet);
        session.DebugSpawnItem(new GridPosition(5, 3), PowerUpKind.Speed);
        session.DebugSpawnItem(new GridPosition(5, 4), PowerUpKind.Kick);

        session.Tick(0.05);

        var nearItem = Assert.Single(session.Items, item => item.Kind == PowerUpKind.Speed);
        var farItem = Assert.Single(session.Items, item => item.Kind == PowerUpKind.Kick);
        Assert.Equal(new GridPosition(5, 3), nearItem.Cell);
        Assert.Equal(5.325, nearItem.X, precision: 6);
        Assert.Equal(3.5, nearItem.Y, precision: 6);
        Assert.Equal(new GridPosition(5, 4), farItem.Cell);
        Assert.Equal(5.5, farItem.X, precision: 6);
        Assert.Equal(4.5, farItem.Y, precision: 6);
    }
}
