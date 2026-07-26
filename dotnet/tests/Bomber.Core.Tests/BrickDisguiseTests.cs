using Xunit;

namespace Bomber.Core.Tests;

public sealed class BrickDisguiseTests
{
    [Fact]
    public void PickupDisguisesOnlyBombsPlacedAfterItWasCollected()
    {
        var session = TestSessionFactory.Create();
        var ordinaryId = session.DebugPlaceBomb(0, new(3, 3), fuse: 5, range: 2);

        session.DebugApplyPowerUp(0, PowerUpKind.BrickDisguise);
        var disguisedId = session.DebugPlaceBomb(0, new(5, 3), fuse: 5, range: 2);

        Assert.True(session.Players.Single(player => player.Id == 0).HasBrickDisguise);
        Assert.False(session.Bombs.Single(bomb => bomb.Id == ordinaryId).IsBrickDisguised);
        Assert.True(session.Bombs.Single(bomb => bomb.Id == disguisedId).IsBrickDisguised);
    }

    [Fact]
    public void DisguiseChangesOnlyTheBombsVisualSnapshotFlag()
    {
        var ordinarySession = TestSessionFactory.Create(seed: 77);
        var disguisedSession = TestSessionFactory.Create(seed: 77);
        disguisedSession.DebugApplyPowerUp(0, PowerUpKind.BrickDisguise);

        ordinarySession.DebugPlaceBomb(0, new(3, 3), fuse: 4.25, range: 3, isPiercing: true, isCluster: true);
        disguisedSession.DebugPlaceBomb(0, new(3, 3), fuse: 4.25, range: 3, isPiercing: true, isCluster: true);

        var ordinary = Assert.Single(ordinarySession.Bombs);
        var disguised = Assert.Single(disguisedSession.Bombs);
        Assert.False(ordinary.IsBrickDisguised);
        Assert.True(disguised.IsBrickDisguised);
        Assert.Equal(ordinary.FuseSeconds, disguised.FuseSeconds);
        Assert.Equal(ordinary.InitialFuseSeconds, disguised.InitialFuseSeconds);
        Assert.Equal(ordinary.FireRange, disguised.FireRange);
        Assert.Equal(ordinary.IsPiercing, disguised.IsPiercing);
        Assert.Equal(ordinary.IsCluster, disguised.IsCluster);
        Assert.Equal(ordinary.FlamePreviewCells, disguised.FlamePreviewCells);
        Assert.Equal(TileType.Floor, disguisedSession.Board[disguised.Cell]);
    }
}
