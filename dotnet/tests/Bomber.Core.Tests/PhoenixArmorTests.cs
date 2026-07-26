using Xunit;

namespace Bomber.Core.Tests;

public sealed class PhoenixArmorTests
{
    [Fact]
    public void PhoenixArmorBlocksOwnFlamesButNotEnemyFlames()
    {
        var session = TestSessionFactory.Create();
        var armoredCell = new GridPosition(5, 5);
        session.DebugApplyPowerUp(0, PowerUpKind.FlamePass);
        session.DebugSetPlayerPosition(0, armoredCell);
        session.DebugSetPlayerPosition(1, new GridPosition(9, 9));

        session.DebugPlaceBomb(0, armoredCell, fuse: 0.01, range: 1);
        session.Tick(0.02);

        Assert.True(session.Players[0].IsAlive);
        Assert.Equal(1, session.Players[0].Health);

        session.DebugPlaceBomb(1, armoredCell, fuse: 0.01, range: 1);
        session.Tick(0.02);

        Assert.False(session.Players[0].IsAlive);
        Assert.True(session.Players[0].IsGhost);
    }
}
