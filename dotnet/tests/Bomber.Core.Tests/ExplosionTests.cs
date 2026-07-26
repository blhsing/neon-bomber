using Xunit;

namespace Bomber.Core.Tests;

public sealed class ExplosionTests
{
    [Fact]
    public void CrossBlastHonorsRangeAndStopsAtSolidWallsAndCrates()
    {
        var session = TestSessionFactory.Create();
        session.DebugSetTile(new(7, 5), TileType.SolidWall);
        session.DebugSetTile(new(5, 7), TileType.Crate);
        session.DebugPlaceBomb(0, new(5, 5), fuse: 0.01, range: 3);
        var previewCells = Assert.Single(session.Bombs).FlamePreviewCells.ToHashSet();

        session.Tick(0.02);

        var flames = session.Flames.Select(flame => flame.Cell).ToHashSet();
        Assert.True(previewCells.SetEquals(flames));
        Assert.Contains(new GridPosition(5, 5), flames);
        Assert.Contains(new GridPosition(2, 5), flames);
        Assert.Contains(new GridPosition(6, 5), flames);
        Assert.DoesNotContain(new GridPosition(7, 5), flames);
        Assert.DoesNotContain(new GridPosition(8, 5), flames);
        Assert.Contains(new GridPosition(5, 7), flames);
        Assert.DoesNotContain(new GridPosition(5, 8), flames);
        Assert.Equal(TileType.SolidWall, session.Board[7, 5]);
        Assert.Equal(TileType.Floor, session.Board[5, 7]);
        Assert.Equal(1, session.Players[0].Statistics.CratesDestroyed);
    }

    [Fact]
    public void DestroyedCrateUsesSeededDropAndBlocksCellsBehindIt()
    {
        var session = TestSessionFactory.Create(seed: 2026, itemDropChance: 1);
        session.DebugSetTile(new(4, 3), TileType.Crate);
        session.DebugPlaceBomb(0, new(3, 3), fuse: 0.01, range: 3);

        session.Tick(0.02);

        var item = Assert.Single(session.Items);
        Assert.Equal(new GridPosition(4, 3), item.Cell);
        Assert.Equal("bomb", item.PowerUpId);
        Assert.DoesNotContain(session.Flames, flame => flame.Cell == new GridPosition(5, 3));
    }

    [Fact]
    public void BlastTriggersASecondBombImmediatelyAndUsesItsOwnRange()
    {
        var session = TestSessionFactory.Create();
        var firstId = session.DebugPlaceBomb(0, new(3, 3), fuse: 0.01, range: 2);
        var secondId = session.DebugPlaceBomb(1, new(5, 3), fuse: 10, range: 2);

        session.Tick(0.02);

        Assert.Empty(session.Bombs);
        Assert.Contains(session.Flames, flame => flame.SourceBombId == firstId);
        Assert.Contains(session.Flames, flame => flame.SourceBombId == secondId);
        Assert.Contains(session.Flames, flame => flame.Cell == new GridPosition(7, 3));
        Assert.All(session.Players, player => Assert.Equal(0, player.ActiveBombs));
    }

    [Fact]
    public void PiercingBlastPassesOneCrateButTheNextCrateStopsIt()
    {
        var session = TestSessionFactory.Create();
        session.DebugSetTile(new(4, 3), TileType.Crate);
        session.DebugSetTile(new(6, 3), TileType.Crate);
        session.DebugPlaceBomb(0, new(3, 3), fuse: 0.01, range: 5, isPiercing: true);

        session.Tick(0.02);

        var flames = session.Flames.Select(flame => flame.Cell).ToHashSet();
        Assert.Contains(new GridPosition(4, 3), flames);
        Assert.Contains(new GridPosition(5, 3), flames);
        Assert.Contains(new GridPosition(6, 3), flames);
        Assert.DoesNotContain(new GridPosition(7, 3), flames);
        Assert.Equal(2, session.Players[0].Statistics.CratesDestroyed);
    }

    [Fact]
    public void ExistingItemIsDestroyedByFlames()
    {
        var session = TestSessionFactory.Create();
        var cell = new GridPosition(5, 5);
        session.DebugSpawnItem(cell, PowerUpKind.Speed);
        session.DebugPlaceBomb(0, cell, fuse: 0.01, range: 1);

        session.Tick(0.02);

        Assert.Empty(session.Items);
    }
}
