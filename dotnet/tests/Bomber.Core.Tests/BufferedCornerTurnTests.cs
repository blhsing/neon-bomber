using Xunit;

namespace Bomber.Core.Tests;

public sealed class BufferedCornerTurnTests
{
    [Theory]
    [InlineData(4.80, 1, 1, 4, 6)]
    [InlineData(6.20, -1, -1, 6, 4)]
    public void HorizontalMovementBuffersAPerpendicularVerticalTurn(
        double startX,
        int forwardX,
        int turnY,
        int cornerX,
        int cornerY)
    {
        var session = CreateOpenSession();
        session.DebugSetTile(new(cornerX, cornerY), TileType.SolidWall);
        session.DebugSetPlayerPosition(0, startX, 5.5);
        Move(session, forwardX, 0, 0.02);
        var beforeTurn = Player(session);

        Move(session, 0, turnY, 0.10);

        var whileCentering = Player(session);
        Assert.Equal(5.5, whileCentering.Y, precision: 6);
        Assert.True((whileCentering.X - beforeTurn.X) * forwardX > 0.25,
            "The current horizontal heading should continue while the vertical turn is buffered.");

        Move(session, 0, turnY, 0.25);

        var afterTurn = Player(session);
        Assert.Equal(5.5, afterTurn.X, precision: 6);
        Assert.True((afterTurn.Y - 5.5) * turnY > 0.25,
            "The buffered vertical turn should execute immediately after reaching lane center.");
    }

    [Theory]
    [InlineData(4.80, 1, 1, 6, 4)]
    [InlineData(6.20, -1, -1, 4, 6)]
    public void VerticalMovementBuffersAPerpendicularHorizontalTurn(
        double startY,
        int forwardY,
        int turnX,
        int cornerX,
        int cornerY)
    {
        var session = CreateOpenSession();
        session.DebugSetTile(new(cornerX, cornerY), TileType.SolidWall);
        session.DebugSetPlayerPosition(0, 5.5, startY);
        Move(session, 0, forwardY, 0.02);
        var beforeTurn = Player(session);

        Move(session, turnX, 0, 0.10);

        var whileCentering = Player(session);
        Assert.Equal(5.5, whileCentering.X, precision: 6);
        Assert.True((whileCentering.Y - beforeTurn.Y) * forwardY > 0.25,
            "The current vertical heading should continue while the horizontal turn is buffered.");

        Move(session, turnX, 0, 0.25);

        var afterTurn = Player(session);
        Assert.Equal(5.5, afterTurn.Y, precision: 6);
        Assert.True((afterTurn.X - 5.5) * turnX > 0.25,
            "The buffered horizontal turn should execute immediately after reaching lane center.");
    }

    [Fact]
    public void BufferedTurningNeverCarriesAPlayerThroughAHeadOnWall()
    {
        var session = CreateOpenSession();
        session.DebugSetTile(new(6, 5), TileType.SolidWall);
        session.DebugSetPlayerPosition(0, 5.20, 5.5);

        Move(session, 1, 0, 0.40);

        var player = Player(session);
        Assert.True(player.X < 5.71, $"A solid wall should stop head-on movement, but X reached {player.X}.");
        Assert.Equal(5.5, player.Y, precision: 6);
    }

    private static GameSession CreateOpenSession()
    {
        var session = TestSessionFactory.Create(fuseSeconds: 10);
        for (var y = 3; y <= 8; y++)
        {
            for (var x = 3; x <= 8; x++)
            {
                session.DebugSetTile(new(x, y), TileType.Floor);
            }
        }

        return session;
    }

    private static void Move(GameSession session, int horizontal, int vertical, double seconds)
    {
        Assert.True(session.SetControls(0, new PlayerControls(horizontal, vertical)));
        session.Tick(seconds);
    }

    private static PlayerSnapshot Player(GameSession session) =>
        Assert.Single(session.Players, player => player.Id == 0);
}
