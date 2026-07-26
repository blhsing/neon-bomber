using Xunit;

namespace Bomber.Core.Tests;

public sealed class MovementClearanceTests
{
    [Fact]
    public void PlayerCanMovePastACornerAfterMoreThanHalfTheirBodyIsClear()
    {
        var session = CreateCornerTestSession(playerY: 6.05);
        var before = Assert.Single(session.Players, player => player.Id == 0);

        Assert.True(session.SetControls(0, new PlayerControls(Horizontal: 1, Vertical: 0)));
        session.Tick(0.10);

        var after = Assert.Single(session.Players, player => player.Id == 0);
        Assert.True(after.X > before.X + 0.25, $"Expected corner clearance to permit movement; X changed from {before.X} to {after.X}.");
    }

    [Theory]
    [InlineData(5.50)]
    [InlineData(6.00)]
    public void WallStillBlocksHeadOnOrOnlyHalfClearMovement(double playerY)
    {
        var session = CreateCornerTestSession(playerY);
        var before = Assert.Single(session.Players, player => player.Id == 0);

        Assert.True(session.SetControls(0, new PlayerControls(Horizontal: 1, Vertical: 0)));
        session.Tick(0.10);

        var after = Assert.Single(session.Players, player => player.Id == 0);
        Assert.Equal(before.X, after.X, precision: 6);
    }

    [Fact]
    public void HalfwayClearRuleIsSymmetricForVerticalMovement()
    {
        var session = CreateCornerTestSession(playerY: 4.65);
        session.DebugSetPlayerPosition(0, x: 6.05, y: 4.65);
        var before = Assert.Single(session.Players, player => player.Id == 0);

        Assert.True(session.SetControls(0, new PlayerControls(Horizontal: 0, Vertical: 1)));
        session.Tick(0.10);

        var after = Assert.Single(session.Players, player => player.Id == 0);
        Assert.True(after.Y > before.Y + 0.25, $"Expected corner clearance to permit movement; Y changed from {before.Y} to {after.Y}.");
    }

    private static GameSession CreateCornerTestSession(double playerY)
    {
        var session = TestSessionFactory.Create(fuseSeconds: 10);
        for (var y = 3; y <= 7; y++)
        {
            for (var x = 3; x <= 8; x++)
            {
                session.DebugSetTile(new(x, y), TileType.Floor);
            }
        }

        session.DebugSetTile(new(5, 5), TileType.SolidWall);
        session.DebugSetPlayerPosition(0, x: 4.65, y: playerY);
        return session;
    }
}
