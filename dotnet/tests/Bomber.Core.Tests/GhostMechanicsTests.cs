using Xunit;

namespace Bomber.Core.Tests;

public sealed class GhostMechanicsTests
{
    [Fact]
    public void DeadHumanMovesOnOuterRailAndThrowsOneAirborneGhostBomb()
    {
        var session = CreateSession(playerCount: 4);
        session.DebugApplyPowerUp(0, PowerUpKind.FireRange);
        session.DebugApplyPowerUp(0, PowerUpKind.Pierce);
        session.DebugSetPlayerPosition(0, x: 5.25, y: 2.25);
        session.DebugEliminatePlayer(0);

        var ghost = session.Players[0];
        Assert.False(ghost.IsAlive);
        Assert.True(ghost.IsGhost);
        Assert.Equal(5.25, ghost.X, precision: 6);
        Assert.Equal(0.5, ghost.Y, precision: 6);
        Assert.Equal(2, ghost.FireRange);
        Assert.True(ghost.HasPiercingFlames);

        Assert.True(session.SetControls(0, new PlayerControls(Horizontal: 1, Vertical: 0)));
        session.Tick(0.10);
        Assert.True(session.Players[0].X > 5.25);
        Assert.Equal(0.5, session.Players[0].Y, precision: 6);

        Assert.True(session.SetControls(0, PlayerControls.None));
        session.Tick(1.0 / 60);
        Assert.True(session.SetControls(0, new PlayerControls(Horizontal: 0, Vertical: 0, PlaceBomb: true)));
        session.Tick(1.0 / 60);

        var bomb = Assert.Single(session.Bombs, candidate => candidate.IsGhost);
        Assert.True(bomb.IsAirborne);
        Assert.Equal(1.9, bomb.FuseSeconds, precision: 6);
        Assert.False(session.Players[0].IsGhostBombReady);
        Assert.Equal(0, session.Players[0].ActiveBombs);
        Assert.Equal(1, session.Players[0].Statistics.GhostBombsThrown);
        Assert.Equal(0, session.Players[0].FacingX);
        Assert.Equal(1, session.Players[0].FacingY);

        var landingCell = bomb.Cell;
        Assert.True(session.SetControls(0, PlayerControls.None));
        session.Tick(1.0 / 60);
        Assert.True(session.SetControls(0, new PlayerControls(Horizontal: 0, Vertical: 0, PlaceBomb: true)));
        session.Tick(1.0 / 60);
        Assert.Single(session.Bombs, candidate => candidate.IsGhost);
        Assert.Equal(1, session.Players[0].Statistics.GhostBombsThrown);
        Assert.Throws<InvalidOperationException>(() =>
            session.DebugPlaceBomb(2, landingCell, fuse: 10, range: 1));

        session.DebugSetPlayerPosition(1, landingCell);

        session.Tick(0.30);
        bomb = Assert.Single(session.Bombs, candidate => candidate.IsGhost);
        Assert.True(bomb.IsAirborne);
        Assert.Equal(1.9, bomb.FuseSeconds, precision: 6);

        session.Tick(0.25);
        bomb = Assert.Single(session.Bombs, candidate => candidate.IsGhost);
        Assert.False(bomb.IsAirborne);
        Assert.True(bomb.FuseSeconds < 1.9);
        Assert.NotEmpty(bomb.FlamePreviewCells);
        Assert.Contains(1, session.DebugBombPassThroughPlayers(bomb.Id));
    }

    [Fact]
    public void GhostRetainsBombCapacityAndBombModifiers()
    {
        var session = CreateSession(playerCount: 4);
        session.DebugApplyPowerUp(0, PowerUpKind.BombCapacity);
        session.DebugApplyPowerUp(0, PowerUpKind.FireRange);
        session.DebugApplyPowerUp(0, PowerUpKind.Remote);
        session.DebugApplyPowerUp(0, PowerUpKind.Pierce);
        session.DebugApplyPowerUp(0, PowerUpKind.BrickDisguise);
        session.DebugApplyPowerUp(0, PowerUpKind.Mega);
        session.DebugApplyPowerUp(0, PowerUpKind.Cluster);
        session.DebugEliminatePlayer(0);

        Assert.True(session.SetControls(0, new PlayerControls(0, 0, PlaceBomb: true)));
        session.Tick(1.0 / 60);

        var first = Assert.Single(session.Bombs, bomb => bomb.IsGhost);
        Assert.Equal(2, first.FireRange);
        Assert.Equal(8, first.FuseSeconds, precision: 6);
        Assert.True(first.IsPiercing);
        Assert.True(first.IsBrickDisguised);
        Assert.True(first.IsMega);
        Assert.True(first.IsCluster);
        Assert.Equal(1, session.Players[0].ActiveGhostBombs);
        Assert.True(session.Players[0].IsGhostBombReady);

        session.Tick(0.55);
        Assert.True(session.SetControls(0, PlayerControls.None));
        session.Tick(1.0 / 60);
        Assert.True(session.SetControls(0, new PlayerControls(0, 0, PlaceBomb: true)));
        session.Tick(1.0 / 60);

        Assert.Equal(2, session.Bombs.Count(bomb => bomb.IsGhost));
        Assert.Equal(2, session.Players[0].ActiveGhostBombs);
        Assert.False(session.Players[0].IsGhostBombReady);
    }

    [Fact]
    public void GhostRetainsSpeedAndCanRemoteDetonateLandedBomb()
    {
        var normal = CreateSession(playerCount: 4);
        var fast = CreateSession(playerCount: 4);
        for (var count = 0; count < 3; count++)
        {
            fast.DebugApplyPowerUp(0, PowerUpKind.Speed);
        }

        normal.DebugEliminatePlayer(0);
        fast.DebugEliminatePlayer(0);
        Assert.True(normal.SetControls(0, new PlayerControls(1, 0)));
        Assert.True(fast.SetControls(0, new PlayerControls(1, 0)));
        normal.Tick(0.10);
        fast.Tick(0.10);
        Assert.True(fast.Players[0].X > normal.Players[0].X);

        fast.DebugApplyPowerUp(0, PowerUpKind.Remote);
        var bombId = fast.DebugPlaceGhostBomb(0, new GridPosition(5, 5), fuse: 8);
        Assert.True(fast.SetControls(0, PlayerControls.None));
        fast.Tick(1.0 / 60);
        Assert.True(fast.SetControls(0, new PlayerControls(0, 0, UseAction: true)));
        fast.Tick(1.0 / 60);

        Assert.DoesNotContain(fast.Bombs, bomb => bomb.Id == bombId);
        Assert.Contains(fast.Flames, flame => flame.SourceBombId == bombId);
    }

    [Fact]
    public void GhostBombKillRevivesOwnerWhenTwoLivingPlayersRemain()
    {
        var session = CreateSession(playerCount: 4);
        session.DebugSetPlayerPosition(1, x: 5.25, y: 5.5);
        session.DebugEliminatePlayer(0);
        session.DebugPlaceGhostBomb(0, new GridPosition(5, 5), fuse: 0.01);

        session.Tick(0.02);

        var revived = session.Players[0];
        var victim = session.Players[1];
        Assert.Equal(GamePhase.Playing, session.Phase);
        Assert.True(revived.IsAlive);
        Assert.False(revived.IsGhost);
        Assert.Equal(5.25, revived.X, precision: 6);
        Assert.Equal(5.5, revived.Y, precision: 6);
        Assert.Equal(1, revived.Health);
        Assert.Equal(2.2, revived.InvulnerabilitySeconds, precision: 6);
        Assert.Equal(1, revived.Statistics.Revivals);
        Assert.False(victim.IsAlive);
        Assert.True(victim.IsGhost);
    }

    [Fact]
    public void FinalGhostKillEndsRoundWithoutRevivingOwner()
    {
        var session = CreateSession(playerCount: 3);
        session.DebugSetPlayerPosition(1, new GridPosition(5, 5));
        session.DebugEliminatePlayer(0);
        session.DebugPlaceGhostBomb(0, new GridPosition(5, 5), fuse: 0.01);

        session.Tick(0.02);

        Assert.Equal(GamePhase.RoundOver, session.Phase);
        Assert.False(session.Players[0].IsAlive);
        Assert.True(session.Players[0].IsGhost);
        Assert.Equal(0, session.Players[0].Statistics.Revivals);
        Assert.Equal(2, session.LastRound?.WinnerPlayerId);
    }

    [Fact]
    public void NormalBombPlacedBeforeDeathCannotReviveItsGhostOwner()
    {
        var session = CreateSession(playerCount: 4);
        session.DebugSetPlayerPosition(1, x: 5.25, y: 5.5);
        session.DebugPlaceBomb(0, new GridPosition(5, 5), fuse: 0.01, range: 1);
        session.DebugEliminatePlayer(0);

        session.Tick(0.02);

        Assert.False(session.Players[0].IsAlive);
        Assert.True(session.Players[0].IsGhost);
        Assert.Equal(0, session.Players[0].Statistics.Revivals);
        Assert.True(session.Players[1].IsGhost);
        Assert.Equal(GamePhase.Playing, session.Phase);
    }

    [Fact]
    public void OverlappingNormalFlameDoesNotEraseGhostRevivalAttribution()
    {
        var session = CreateSession(playerCount: 4);
        session.DebugSetPlayerPosition(1, x: 6.25, y: 5.5);
        session.DebugEliminatePlayer(0);
        session.DebugPlaceGhostBomb(0, new GridPosition(5, 5), fuse: 0.01);
        session.DebugPlaceBomb(2, new GridPosition(7, 5), fuse: 0.01, range: 1);

        session.Tick(0.02);

        Assert.True(session.Players[0].IsAlive);
        Assert.False(session.Players[0].IsGhost);
        Assert.Equal(1, session.Players[0].Statistics.Revivals);
        Assert.True(session.Players[1].IsGhost);
        var overlappingFlames = session.Flames.Where(flame => flame.Cell == new GridPosition(6, 5)).ToArray();
        Assert.Contains(overlappingFlames, flame => flame.IsGhostSource);
        Assert.Contains(overlappingFlames, flame => !flame.IsGhostSource);
    }

    [Fact]
    public void ComputerGhostTracksTheRailAndThrowsAtLivingPlayers()
    {
        var session = CreateSession(playerCount: 4, firstPlayerComputer: true);
        for (var playerId = 1; playerId < 4; playerId++)
        {
            session.DebugApplyPowerUp(playerId, PowerUpKind.FlamePass);
        }

        session.DebugSetPlayerPosition(0, new GridPosition(7, 5));
        session.DebugEliminatePlayer(0);
        var start = session.Players[0];

        for (var step = 0; step < 8 * 60; step++)
        {
            session.Tick(1.0 / 60);
        }

        var ghost = session.Players[0];
        Assert.True(ghost.IsGhost);
        Assert.True(
            ghost.Statistics.GhostBombsThrown > 0 || Math.Abs(ghost.X - start.X) > 0.1 || Math.Abs(ghost.Y - start.Y) > 0.1,
            "The computer ghost neither moved nor attempted a throw.");
        Assert.True(ghost.Statistics.GhostBombsThrown > 0, "The computer ghost never reached a firing position.");
    }

    [Fact]
    public void StartingNextRoundClearsGhostState()
    {
        var session = CreateSession(playerCount: 2);
        session.DebugEliminatePlayer(1);
        session.Tick(0.02);

        Assert.True(session.Players[1].IsGhost);
        session.StartNextRound();

        Assert.All(session.Players, player =>
        {
            Assert.True(player.IsAlive);
            Assert.False(player.IsGhost);
            Assert.False(player.IsGhostBombReady);
        });
    }

    private static GameSession CreateSession(int playerCount, bool firstPlayerComputer = false)
    {
        var players = Enumerable.Range(0, playerCount)
            .Select(index => new PlayerSlotConfiguration
            {
                Name = $"Player {index + 1}",
                Kind = index == 0 && firstPlayerComputer ? PlayerKind.Computer : PlayerKind.Human,
                Difficulty = AiDifficulty.Expert
            })
            .ToArray();
        var session = new GameSession(new GameConfiguration
        {
            Seed = 2026,
            TargetCrowns = 9,
            CrateDensity = 0,
            ItemDropChance = 0,
            BombFuseSeconds = 1.9,
            FlameLifetimeSeconds = 0.6,
            SpawnProtectionSeconds = 0,
            Players = players
        });
        session.StartMatch();
        return session;
    }
}
