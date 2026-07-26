namespace Bomber.Core;

public sealed partial class GameSession
{
    private const double GhostTrackWidth = ArenaRules.Width - 1;
    private const double GhostTrackHeight = ArenaRules.Height - 1;
    private const double GhostTrackLength = (GhostTrackWidth * 2) + (GhostTrackHeight * 2);
    private const double GhostMoveSpeed = 4.4;
    private const double GhostBombFuseSeconds = 1.9;
    private const double GhostBombFlightSeconds = 0.48;

    private void UpdateGhostPlayer(PlayerState player, double deltaSeconds)
    {
        if (!player.IsGhost || _players.Count(candidate => candidate.IsAlive) < 2)
        {
            return;
        }

        var horizontal = 0.0;
        var vertical = 0.0;
        var throwBomb = false;
        var useAction = false;
        if (player.Kind == PlayerKind.Computer)
        {
            (horizontal, vertical, throwBomb, useAction) = GetGhostAiActions(player);
        }
        else
        {
            horizontal = player.Controls.Horizontal;
            vertical = player.Controls.Vertical;
            throwBomb = player.BombRequested;
            useAction = player.ActionRequested;
            player.BombRequested = false;
            player.ActionRequested = false;
        }

        MoveGhost(player, horizontal, vertical, deltaSeconds);
        if (throwBomb)
        {
            TryThrowGhostBomb(player);
        }

        if (useAction)
        {
            UseGhostAction(player);
        }
    }

    private (double Horizontal, double Vertical, bool ThrowBomb, bool UseAction) GetGhostAiActions(PlayerState player)
    {
        var living = _players.Where(candidate => candidate.IsAlive).ToArray();
        if (living.Length < 2)
        {
            player.AiIntent = "Ghost idle";
            return default;
        }

        var currentTarget = player.GhostTargetPlayerId is { } targetId
            ? living.FirstOrDefault(candidate => candidate.Id == targetId)
            : null;
        if (player.GhostThinkRemaining <= 0 || currentTarget is null || !player.HasGhostAim)
        {
            var candidates = living
                .SelectMany(target => GhostFiringPosts(target).Select(post => new
                {
                    Target = target,
                    Post = post,
                    Distance = Math.Abs(GhostTrackDelta(player.GhostTrack, post.Track)),
                    HasLandingCell = GetGhostLandingCells(post.Track, post.FacingX, post.FacingY).Count > 0
                }))
                .Where(candidate => candidate.HasLandingCell)
                .OrderBy(candidate => candidate.Distance)
                .ThenBy(candidate => candidate.Target.Id)
                .ThenBy(candidate => candidate.Post.Track)
                .ToArray();
            var chosen = candidates.FirstOrDefault();
            if (chosen is null)
            {
                player.HasGhostAim = false;
                player.GhostTargetPlayerId = null;
                player.AiIntent = "Ghost idle";
                return default;
            }

            player.GhostTargetPlayerId = chosen.Target.Id;
            player.GhostAimTrack = chosen.Post.Track;
            player.GhostAimFacingX = chosen.Post.FacingX;
            player.GhostAimFacingY = chosen.Post.FacingY;
            player.HasGhostAim = true;
            player.GhostThinkRemaining = player.Difficulty switch
            {
                AiDifficulty.Novice => 0.55,
                AiDifficulty.Expert => 0.16,
                _ => 0.30
            };
        }

        var delta = GhostTrackDelta(player.GhostTrack, player.GhostAimTrack);
        if (Math.Abs(delta) > 0.16)
        {
            player.AiIntent = "Ghost tracking";
            var current = GhostPoint(player.GhostTrack);
            var next = GhostPoint(player.GhostTrack + (Math.Sign(delta) * 0.08));
            var useDash = !player.HasRemote && player.DashCharges > 0 && player.DashTime <= 0 && Math.Abs(delta) > 3;
            return (next.X - current.X, next.Y - current.Y, false, useDash);
        }

        player.GhostTrack = WrapGhostTrack(player.GhostAimTrack);
        var position = GhostPoint(player.GhostTrack);
        player.X = position.X;
        player.Y = position.Y;
        player.FacingX = player.GhostAimFacingX;
        player.FacingY = player.GhostAimFacingY;
        var activeBombs = ActiveGhostBombs(player).ToArray();
        var useRemote = player.HasRemote && activeBombs.Length >= player.BombCapacity &&
            activeBombs.Any(bomb => !bomb.IsAirborne);
        var canThrow = activeBombs.Length < player.BombCapacity && activeBombs.All(bomb => !bomb.IsAirborne);
        player.AiIntent = useRemote ? "Ghost remote detonation" : canThrow ? "Ghost ambush" : "Ghost waiting";
        return (player.FacingX, player.FacingY, canThrow, useRemote);
    }

    private void MoveGhost(PlayerState player, double horizontal, double vertical, double deltaSeconds)
    {
        var magnitude = Math.Sqrt((horizontal * horizontal) + (vertical * vertical));
        if (magnitude <= 0.001)
        {
            return;
        }

        if (Math.Abs(horizontal) > Math.Abs(vertical))
        {
            player.FacingX = Math.Sign(horizontal);
            player.FacingY = 0;
        }
        else
        {
            player.FacingX = 0;
            player.FacingY = Math.Sign(vertical);
        }

        horizontal /= magnitude;
        vertical /= magnitude;
        var current = GhostPoint(player.GhostTrack);
        var plus = GhostPoint(player.GhostTrack + 0.06);
        var minus = GhostPoint(player.GhostTrack - 0.06);
        var plusScore = (horizontal * (plus.X - current.X)) + (vertical * (plus.Y - current.Y));
        var minusScore = (horizontal * (minus.X - current.X)) + (vertical * (minus.Y - current.Y));
        if (Math.Max(plusScore, minusScore) > 0.003)
        {
            var direction = plusScore > minusScore ? 1 : -1;
            var speedScale = player.MoveSpeed / 3.15;
            var dashScale = player.DashTime > 0 ? 2.15 : 1;
            player.GhostTrack = WrapGhostTrack(player.GhostTrack +
                (direction * GhostMoveSpeed * speedScale * dashScale * deltaSeconds));
        }

        var position = GhostPoint(player.GhostTrack);
        player.X = position.X;
        player.Y = position.Y;
    }

    private bool TryThrowGhostBomb(PlayerState player)
    {
        if (!player.IsGhost || player.IsAlive || _players.Count(candidate => candidate.IsAlive) < 2 ||
            player.ActiveGhostBombs >= player.BombCapacity)
        {
            return false;
        }

        var railPosition = GhostPoint(player.GhostTrack);
        player.FacingX = railPosition.InwardX;
        player.FacingY = railPosition.InwardY;
        var landingCells = GetGhostLandingCells(player.GhostTrack, player.FacingX, player.FacingY);
        if (landingCells.Count == 0)
        {
            return false;
        }

        var target = landingCells[_random.NextInt(landingCells.Count)];
        var isMega = player.MegaCharges > 0;
        var isCluster = player.ClusterCharges > 0;
        if (isMega) player.MegaCharges--;
        if (isCluster) player.ClusterCharges--;
        var fuse = player.HasRemote ? 8 : GhostBombFuseSeconds;
        var bomb = CreateGhostBomb(
            player,
            target,
            fuse,
            player.FireRange,
            airborne: true,
            isMega: isMega,
            isCluster: isCluster);
        bomb.GhostLandingCandidates.AddRange(landingCells);
        player.ActiveGhostBombs++;
        player.Statistics.GhostBombsThrown++;
        return true;
    }

    private IEnumerable<BombState> ActiveGhostBombs(PlayerState player) =>
        _bombs.Where(bomb => !bomb.IsExploded && bomb.IsGhost && bomb.OwnerPlayerId == player.Id);

    private void UseGhostAction(PlayerState player)
    {
        if (player.HasRemote)
        {
            var oldest = ActiveGhostBombs(player)
                .Where(bomb => !bomb.IsAirborne)
                .OrderBy(bomb => bomb.Id)
                .FirstOrDefault();
            if (oldest is not null)
            {
                oldest.Fuse = 0;
            }

            return;
        }

        if (player.DashCharges > 0 && player.DashTime <= 0)
        {
            player.DashCharges--;
            player.DashTime = 0.34;
        }
    }

    private BombState CreateGhostBomb(
        PlayerState owner,
        GridPosition cell,
        double fuse,
        int range,
        bool airborne,
        bool isMega = false,
        bool isCluster = false)
    {
        var bomb = new BombState
        {
            Id = ++_nextBombId,
            OwnerPlayerId = owner.Id,
            Cell = cell,
            Fuse = fuse,
            InitialFuse = Math.Max(fuse, 0.001),
            Range = Math.Clamp(range, 1, PlayerCaps.FireRange),
            IsMega = isMega,
            IsCluster = isCluster,
            IsPiercing = owner.HasPiercingFlames,
            IsBrickDisguised = owner.HasBrickDisguise,
            IsGhost = true,
            SourceGhostGeneration = owner.GhostGeneration,
            AirborneDuration = airborne ? GhostBombFlightSeconds : 0,
            AirborneFromX = owner.X,
            AirborneFromY = owner.Y
        };
        _bombs.Add(bomb);
        return bomb;
    }

    private void LandGhostBomb(BombState bomb)
    {
        var originalTarget = bomb.Cell;
        var landingCells = bomb.GhostLandingCandidates
            .Where(cell => _board[cell] == TileType.Floor && !IsBombReserved(cell, bomb))
            .ToList();
        var target = landingCells
            .OrderBy(cell => ManhattanDistance(cell, originalTarget))
            .ThenBy(cell => cell.Y)
            .ThenBy(cell => cell.X)
            .FirstOrDefault();
        if (landingCells.Count == 0)
        {
            var fallback = new List<GridPosition>();
            for (var y = 1; y < ArenaRules.Height - 1; y++)
            {
                for (var x = 1; x < ArenaRules.Width - 1; x++)
                {
                    var cell = new GridPosition(x, y);
                    if (_board[cell] == TileType.Floor && !IsBombReserved(cell, bomb))
                    {
                        fallback.Add(cell);
                    }
                }
            }

            if (fallback.Count == 0)
            {
                CancelGhostBomb(bomb);
                return;
            }

            target = fallback
                .OrderBy(cell => ManhattanDistance(cell, originalTarget))
                .ThenBy(cell => cell.Y)
                .ThenBy(cell => cell.X)
                .First();
        }

        bomb.Cell = target;
        bomb.AirborneElapsed = bomb.AirborneDuration;
        foreach (var player in _players.Where(candidate => candidate.IsAlive &&
                     CircleOverlapsCell(candidate.X, candidate.Y, PlayerRadius, target)))
        {
            bomb.PassThroughPlayers.Add(player.Id);
        }
    }

    private List<GridPosition> GetGhostLandingCells(
        double track,
        int facingX,
        int facingY,
        BombState? exclude = null)
    {
        var position = GhostPoint(track);
        var cells = new List<GridPosition>();
        if (position.Segment == GhostTrackSegment.Top && facingX == 0 && facingY == 1)
        {
            var x = Math.Clamp((int)Math.Floor(position.X), 1, ArenaRules.Width - 2);
            for (var y = 1; y < ArenaRules.Height - 1; y++) cells.Add(new(x, y));
        }
        else if (position.Segment == GhostTrackSegment.Bottom && facingX == 0 && facingY == -1)
        {
            var x = Math.Clamp((int)Math.Floor(position.X), 1, ArenaRules.Width - 2);
            for (var y = ArenaRules.Height - 2; y >= 1; y--) cells.Add(new(x, y));
        }
        else if (position.Segment == GhostTrackSegment.Left && facingX == 1 && facingY == 0)
        {
            var y = Math.Clamp((int)Math.Floor(position.Y), 1, ArenaRules.Height - 2);
            for (var x = 1; x < ArenaRules.Width - 1; x++) cells.Add(new(x, y));
        }
        else if (position.Segment == GhostTrackSegment.Right && facingX == -1 && facingY == 0)
        {
            var y = Math.Clamp((int)Math.Floor(position.Y), 1, ArenaRules.Height - 2);
            for (var x = ArenaRules.Width - 2; x >= 1; x--) cells.Add(new(x, y));
        }

        return cells
            .Where(cell => _board[cell] == TileType.Floor && !IsBombReserved(cell, exclude))
            .ToList();
    }

    private bool IsBombReserved(GridPosition cell, BombState? exclude = null) =>
        _bombs.Any(bomb => !bomb.IsExploded && bomb != exclude && bomb.Cell == cell);

    private void CancelGhostBomb(BombState bomb)
    {
        bomb.IsExploded = true;
        _usedGhostRevivalSources.Add(bomb.Id);
        var owner = FindPlayer(bomb.OwnerPlayerId);
        if (owner is not null)
        {
            owner.ActiveGhostBombs = Math.Max(0, owner.ActiveGhostBombs - 1);
        }
    }

    private void BecomeGhost(PlayerState player, double deathX, double deathY)
    {
        player.IsAlive = false;
        player.IsGhost = true;
        player.GhostGeneration++;
        player.Health = 0;
        player.Invulnerability = 0;
        player.Frozen = 0;
        player.Reversed = 0;
        player.Slowed = 0;
        player.DashTime = 0;
        player.ClearBufferedTurn();
        player.BombRequested = false;
        player.ActionRequested = false;
        player.AiBombRequested = false;
        player.AiActionRequested = false;
        player.AiRoute.Clear();
        player.AiIntent = "Ghost tracking";
        player.GhostThinkRemaining = 0;
        player.GhostTargetPlayerId = null;
        player.HasGhostAim = false;
        player.ActiveGhostBombs = 0;
        foreach (var bomb in _bombs)
        {
            bomb.PassThroughPlayers.Remove(player.Id);
        }

        player.GhostTrack = GhostTrackFromPosition(deathX, deathY);
        var position = GhostPoint(player.GhostTrack);
        player.X = position.X;
        player.Y = position.Y;
        player.FacingX = position.InwardX;
        player.FacingY = position.InwardY;
    }

    private void TryResolveGhostRevival(
        PlayerState victim,
        FlameState source,
        double deathX,
        double deathY)
    {
        if (!source.IsGhostSource || _usedGhostRevivalSources.Contains(source.SourceBombId))
        {
            return;
        }

        var owner = FindPlayer(source.SourcePlayerId);
        if (owner is null || owner.Id == victim.Id || owner.IsAlive || !owner.IsGhost ||
            owner.GhostGeneration != source.SourceGhostGeneration)
        {
            return;
        }

        _usedGhostRevivalSources.Add(source.SourceBombId);
        ReviveGhost(owner, deathX, deathY);
    }

    private void ReviveGhost(PlayerState player, double x, double y)
    {
        foreach (var bomb in _bombs.Where(candidate => !candidate.IsExploded && candidate.IsGhost &&
                     candidate.OwnerPlayerId == player.Id))
        {
            CancelGhostBomb(bomb);
        }

        foreach (var flame in _flames.Where(candidate => candidate.IsGhostSource &&
                     candidate.SourcePlayerId == player.Id))
        {
            _usedGhostRevivalSources.Add(flame.SourceBombId);
        }

        player.IsAlive = true;
        player.IsGhost = false;
        player.X = x;
        player.Y = y;
        player.Health = 1;
        player.Invulnerability = 2.2;
        player.Frozen = 0;
        player.Reversed = 0;
        player.Slowed = 0;
        player.DashTime = 0;
        player.ClearBufferedTurn();
        player.GhostTargetPlayerId = null;
        player.HasGhostAim = false;
        player.ActiveGhostBombs = 0;
        player.AiThinkRemaining = 0;
        player.AiRoute.Clear();
        player.AiIntent = "Revived";
        player.ActiveBombs = _bombs.Count(candidate => !candidate.IsExploded && !candidate.IsGhost &&
            candidate.OwnerPlayerId == player.Id);
        foreach (var bomb in _bombs.Where(candidate => !candidate.IsExploded && !candidate.IsAirborne &&
                     CircleOverlapsCell(player.X, player.Y, PlayerRadius, candidate.Cell)))
        {
            bomb.PassThroughPlayers.Add(player.Id);
        }

        player.Statistics.Revivals++;
    }

    private double GhostTrackFromPosition(double x, double y)
    {
        var choices = new (double Distance, double Track)[]
        {
            (y - 0.5, Math.Clamp(x - 0.5, 0, GhostTrackWidth)),
            (ArenaRules.Width - 0.5 - x, GhostTrackWidth + Math.Clamp(y - 0.5, 0, GhostTrackHeight)),
            (ArenaRules.Height - 0.5 - y, GhostTrackWidth + GhostTrackHeight + GhostTrackWidth - Math.Clamp(x - 0.5, 0, GhostTrackWidth)),
            (x - 0.5, GhostTrackLength - Math.Clamp(y - 0.5, 0, GhostTrackHeight))
        };
        return choices.OrderBy(choice => choice.Distance).First().Track;
    }

    private static double WrapGhostTrack(double track) =>
        ((track % GhostTrackLength) + GhostTrackLength) % GhostTrackLength;

    private static double GhostTrackDelta(double from, double to)
    {
        var delta = WrapGhostTrack(to) - WrapGhostTrack(from);
        if (delta > GhostTrackLength / 2) delta -= GhostTrackLength;
        if (delta < -GhostTrackLength / 2) delta += GhostTrackLength;
        return delta;
    }

    private static GhostTrackPoint GhostPoint(double track)
    {
        var value = WrapGhostTrack(track);
        if (value <= GhostTrackWidth)
        {
            return new(0.5 + value, 0.5, GhostTrackSegment.Top, 0, 1);
        }

        value -= GhostTrackWidth;
        if (value <= GhostTrackHeight)
        {
            return new(ArenaRules.Width - 0.5, 0.5 + value, GhostTrackSegment.Right, -1, 0);
        }

        value -= GhostTrackHeight;
        if (value <= GhostTrackWidth)
        {
            return new(ArenaRules.Width - 0.5 - value, ArenaRules.Height - 0.5, GhostTrackSegment.Bottom, 0, -1);
        }

        value -= GhostTrackWidth;
        return new(0.5, ArenaRules.Height - 0.5 - value, GhostTrackSegment.Left, 1, 0);
    }

    private static IEnumerable<GhostFiringPost> GhostFiringPosts(PlayerState target)
    {
        var x = Math.Clamp(target.Cell.X, 1, ArenaRules.Width - 2);
        var y = Math.Clamp(target.Cell.Y, 1, ArenaRules.Height - 2);
        yield return new(x, 0, 1);
        yield return new(GhostTrackWidth + y, -1, 0);
        yield return new(GhostTrackWidth + GhostTrackHeight + GhostTrackWidth - x, 0, -1);
        yield return new(GhostTrackLength - y, 1, 0);
    }

    internal long DebugPlaceGhostBomb(int playerId, GridPosition cell, double fuse = GhostBombFuseSeconds)
    {
        var player = FindPlayer(playerId) ?? throw new ArgumentOutOfRangeException(nameof(playerId));
        if (!player.IsGhost || player.IsAlive || _board[cell] != TileType.Floor || IsBombReserved(cell))
        {
            throw new InvalidOperationException("A debug ghost bomb requires a ghost owner and an empty floor cell.");
        }

        var bomb = CreateGhostBomb(player, cell, fuse, player.FireRange, airborne: false);
        player.ActiveGhostBombs++;
        player.Statistics.GhostBombsThrown++;
        return bomb.Id;
    }

    private enum GhostTrackSegment
    {
        Top,
        Right,
        Bottom,
        Left
    }

    private sealed record GhostTrackPoint(
        double X,
        double Y,
        GhostTrackSegment Segment,
        int InwardX,
        int InwardY);

    private sealed record GhostFiringPost(double Track, int FacingX, int FacingY);
}
