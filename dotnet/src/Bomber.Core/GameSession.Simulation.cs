namespace Bomber.Core;

public sealed partial class GameSession
{
    private const double PlayerRadius = 0.30;
    private const double TurnBufferHeadingLifetime = 0.18;
    private const double LaneCenterTolerance = 0.002;
    private const double MagnetAttractionRadius = 2.5;

    private void Step(double deltaSeconds)
    {
        _elapsedSeconds += deltaSeconds;
        UpdateStatusTimers(deltaSeconds);
        UpdatePlayers(deltaSeconds);
        UpdateBombPassers();
        UpdateBombs(deltaSeconds);
        UpdateFlames(deltaSeconds);
        UpdateItems(deltaSeconds);
        EvaluateRoundEnd();
        Version++;
    }

    private void UpdateStatusTimers(double deltaSeconds)
    {
        foreach (var player in _players)
        {
            player.Invulnerability = Math.Max(0, player.Invulnerability - deltaSeconds);
            player.Frozen = Math.Max(0, player.Frozen - deltaSeconds);
            player.Reversed = Math.Max(0, player.Reversed - deltaSeconds);
            player.Slowed = Math.Max(0, player.Slowed - deltaSeconds);
            player.DashTime = Math.Max(0, player.DashTime - deltaSeconds);
            player.AiThinkRemaining -= deltaSeconds;
            player.GhostThinkRemaining -= deltaSeconds;
        }
    }

    private void UpdatePlayers(double deltaSeconds)
    {
        foreach (var player in _players)
        {
            if (!player.IsAlive)
            {
                if (player.IsGhost)
                {
                    UpdateGhostPlayer(player, deltaSeconds);
                }

                continue;
            }

            var horizontal = 0.0;
            var vertical = 0.0;
            var placeBomb = false;
            var useAction = false;
            if (player.Kind == PlayerKind.Computer)
            {
                UpdateAi(player);
                (horizontal, vertical) = GetAiMovement(player);
                placeBomb = player.AiBombRequested;
                useAction = player.AiActionRequested;
                player.AiBombRequested = false;
                player.AiActionRequested = false;
            }
            else
            {
                horizontal = player.Controls.Horizontal;
                vertical = player.Controls.Vertical;
                placeBomb = player.BombRequested;
                useAction = player.ActionRequested;
                player.BombRequested = false;
                player.ActionRequested = false;
            }

            if (player.Frozen > 0)
            {
                continue;
            }

            if (player.Reversed > 0)
            {
                horizontal = -horizontal;
                vertical = -vertical;
            }

            MovePlayer(player, horizontal, vertical, deltaSeconds);
            if (placeBomb)
            {
                TryPlaceBomb(player);
            }

            if (useAction)
            {
                UseAction(player);
            }
        }
    }

    private void MovePlayer(PlayerState player, double horizontal, double vertical, double deltaSeconds)
    {
        player.MovementIdleSeconds += deltaSeconds;
        var magnitude = Math.Sqrt((horizontal * horizontal) + (vertical * vertical));
        if (magnitude <= 0.001)
        {
            player.ClearBufferedTurn();
            return;
        }

        if (magnitude > 1)
        {
            horizontal /= magnitude;
            vertical /= magnitude;
        }

        var multiplier = player.DashTime > 0 ? 2.15 : 1;
        if (player.Slowed > 0)
        {
            multiplier *= 0.58;
        }

        var distance = player.MoveSpeed * multiplier * deltaSeconds;
        var requestedX = Math.Abs(horizontal) > 0.05 && Math.Abs(vertical) <= 0.05 ? Math.Sign(horizontal) : 0;
        var requestedY = Math.Abs(vertical) > 0.05 && Math.Abs(horizontal) <= 0.05 ? Math.Sign(vertical) : 0;
        if (player.Kind == PlayerKind.Human && (requestedX != 0 || requestedY != 0) &&
            TryMoveBufferedTurn(player, requestedX, requestedY, distance))
        {
            return;
        }

        player.ClearBufferedTurn();
        if (Math.Abs(horizontal) >= Math.Abs(vertical) && Math.Abs(horizontal) > 0.05)
        {
            player.FacingX = Math.Sign(horizontal);
            player.FacingY = 0;
        }
        else if (Math.Abs(vertical) > 0.05)
        {
            player.FacingX = 0;
            player.FacingY = Math.Sign(vertical);
        }

        var movedHorizontally = TryMoveAxis(player, horizontal * distance, 0);
        var movedVertically = TryMoveAxis(player, 0, vertical * distance);
        if (movedHorizontally || movedVertically)
        {
            RememberMovement(
                player,
                movedHorizontally && (!movedVertically || Math.Abs(horizontal) >= Math.Abs(vertical)) ? Math.Sign(horizontal) : 0,
                movedVertically && (!movedHorizontally || Math.Abs(vertical) > Math.Abs(horizontal)) ? Math.Sign(vertical) : 0);
        }
    }

    private bool TryMoveBufferedTurn(PlayerState player, int requestedX, int requestedY, double distance)
    {
        if (player.BufferedTurnX != 0 || player.BufferedTurnY != 0)
        {
            if (player.BufferedTurnX != requestedX || player.BufferedTurnY != requestedY)
            {
                player.ClearBufferedTurn();
                return false;
            }

            return AdvanceBufferedTurn(player, distance);
        }

        var forwardX = player.RecentMoveX;
        var forwardY = player.RecentMoveY;
        if (player.MovementIdleSeconds > TurnBufferHeadingLifetime ||
            (forwardX * requestedX) + (forwardY * requestedY) != 0 ||
            (forwardX == 0 && forwardY == 0))
        {
            return false;
        }

        var coordinate = forwardX != 0 ? player.X : player.Y;
        var nearestCenter = Math.Floor(coordinate) + 0.5;
        if (Math.Abs(coordinate - nearestCenter) <= LaneCenterTolerance)
        {
            return false;
        }

        var forwardSign = forwardX != 0 ? forwardX : forwardY;
        var targetCenter = forwardSign > 0
            ? (coordinate < nearestCenter ? nearestCenter : nearestCenter + 1.0)
            : (coordinate > nearestCenter ? nearestCenter : nearestCenter - 1.0);
        var probeDistance = PlayerRadius + 0.02;
        var requestedProbeX = player.X + (requestedX * probeDistance);
        var requestedProbeY = player.Y + (requestedY * probeDistance);
        if (!HasRawBlockingOverlap(player, requestedProbeX, requestedProbeY))
        {
            return false;
        }

        var targetX = forwardX != 0 ? targetCenter : player.X;
        var targetY = forwardY != 0 ? targetCenter : player.Y;
        if (!CanOccupy(player, targetX, targetY, forwardX, forwardY) ||
            !CanOccupy(
                player,
                targetX + (requestedX * probeDistance),
                targetY + (requestedY * probeDistance),
                requestedX,
                requestedY))
        {
            return false;
        }

        player.BufferedTurnX = requestedX;
        player.BufferedTurnY = requestedY;
        player.BufferedForwardX = forwardX;
        player.BufferedForwardY = forwardY;
        player.BufferedTurnTarget = targetCenter;
        return AdvanceBufferedTurn(player, distance);
    }

    private bool AdvanceBufferedTurn(PlayerState player, double distance)
    {
        var forwardX = player.BufferedForwardX;
        var forwardY = player.BufferedForwardY;
        var coordinate = forwardX != 0 ? player.X : player.Y;
        var gap = player.BufferedTurnTarget - coordinate;
        var forwardDistance = Math.Min(distance, Math.Abs(gap));
        if (forwardDistance > LaneCenterTolerance &&
            !TryMoveAxis(player, forwardX * forwardDistance, forwardY * forwardDistance))
        {
            player.ClearBufferedTurn();
            return false;
        }

        if (forwardDistance > LaneCenterTolerance)
        {
            RememberMovement(player, forwardX, forwardY);
        }

        var remainingDistance = Math.Max(0, distance - forwardDistance);
        coordinate = forwardX != 0 ? player.X : player.Y;
        if (Math.Abs(player.BufferedTurnTarget - coordinate) > LaneCenterTolerance)
        {
            return true;
        }

        var turnX = player.BufferedTurnX;
        var turnY = player.BufferedTurnY;
        player.ClearBufferedTurn();
        player.FacingX = turnX;
        player.FacingY = turnY;
        if (remainingDistance > LaneCenterTolerance &&
            TryMoveAxis(player, turnX * remainingDistance, turnY * remainingDistance))
        {
            RememberMovement(player, turnX, turnY);
        }

        return true;
    }

    private static void RememberMovement(PlayerState player, int directionX, int directionY)
    {
        player.RecentMoveX = directionX;
        player.RecentMoveY = directionY;
        player.MovementIdleSeconds = 0;
    }

    private bool HasRawBlockingOverlap(PlayerState player, double x, double y)
    {
        var minimumX = (int)Math.Floor(x - PlayerRadius);
        var maximumX = (int)Math.Floor(x + PlayerRadius);
        var minimumY = (int)Math.Floor(y - PlayerRadius);
        var maximumY = (int)Math.Floor(y + PlayerRadius);
        for (var cellY = minimumY; cellY <= maximumY; cellY++)
        {
            for (var cellX = minimumX; cellX <= maximumX; cellX++)
            {
                var cell = new GridPosition(cellX, cellY);
                var tile = _board[cell];
                if ((tile == TileType.SolidWall || (tile == TileType.Crate && !player.CanPassCrates)) &&
                    CircleOverlapsCell(x, y, PlayerRadius, cell))
                {
                    return true;
                }

                var bomb = FindBomb(cell);
                if (bomb is not null && !player.CanPassBombs && !bomb.PassThroughPlayers.Contains(player.Id) &&
                    CircleOverlapsCell(x, y, PlayerRadius, cell))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryMoveAxis(PlayerState player, double deltaX, double deltaY)
    {
        if (Math.Abs(deltaX) < 1e-9 && Math.Abs(deltaY) < 1e-9)
        {
            return false;
        }

        var nextX = player.X + deltaX;
        var nextY = player.Y + deltaY;
        if (CanOccupy(player, nextX, nextY, deltaX, deltaY))
        {
            player.X = nextX;
            player.Y = nextY;
            return true;
        }

        if (player.CanKick && TryKickAhead(player, Math.Sign(deltaX), Math.Sign(deltaY)) &&
            CanOccupy(player, nextX, nextY, deltaX, deltaY))
        {
            player.X = nextX;
            player.Y = nextY;
            return true;
        }

        return false;
    }

    private bool CanOccupy(PlayerState player, double x, double y, double movementX = 0, double movementY = 0)
    {
        var minimumX = (int)Math.Floor(x - PlayerRadius);
        var maximumX = (int)Math.Floor(x + PlayerRadius);
        var minimumY = (int)Math.Floor(y - PlayerRadius);
        var maximumY = (int)Math.Floor(y + PlayerRadius);
        for (var cellY = minimumY; cellY <= maximumY; cellY++)
        {
            for (var cellX = minimumX; cellX <= maximumX; cellX++)
            {
                var tile = _board[cellX, cellY];
                var cell = new GridPosition(cellX, cellY);
                if ((tile == TileType.SolidWall || (tile == TileType.Crate && !player.CanPassCrates)) &&
                    BlocksDirectionalMovement(x, y, cell, movementX, movementY) &&
                    !MovesOutOfBlockingOverlap(player, x, y, cell))
                {
                    return false;
                }

                var bomb = FindBomb(cell);
                if (bomb is not null && !player.CanPassBombs && !bomb.PassThroughPlayers.Contains(player.Id) &&
                    BlocksDirectionalMovement(x, y, bomb.Cell, movementX, movementY) &&
                    !MovesOutOfBlockingOverlap(player, x, y, bomb.Cell))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool MovesOutOfBlockingOverlap(
        PlayerState player,
        double nextX,
        double nextY,
        GridPosition cell)
    {
        var currentDistanceSquared = SquaredDistanceToCell(player.X, player.Y, cell);
        if (currentDistanceSquared >= PlayerRadius * PlayerRadius)
        {
            return false;
        }

        // The half-clear lane rule can leave the edge of a player's circle slightly inside a
        // neighboring wall, crate, or bomb. Permit movement that strictly reduces that existing
        // overlap; otherwise a small centering step can be rejected forever.
        var nextDistanceSquared = SquaredDistanceToCell(nextX, nextY, cell);
        return nextDistanceSquared > currentDistanceSquared + 1e-12;
    }

    private static bool BlocksDirectionalMovement(
        double x,
        double y,
        GridPosition cell,
        double movementX,
        double movementY)
    {
        if (!CircleOverlapsCell(x, y, PlayerRadius, cell))
        {
            return false;
        }

        // During cardinal movement, a corner only blocks while the player's centerline still
        // crosses the obstacle on the perpendicular axis. Once the centerline is past its edge,
        // more than half of the player's body is clear and parallel movement can continue.
        if (Math.Abs(movementX) > Math.Abs(movementY))
        {
            return y >= cell.Y && y <= cell.Y + 1.0;
        }

        if (Math.Abs(movementY) > 0)
        {
            return x >= cell.X && x <= cell.X + 1.0;
        }

        return true;
    }

    private bool TryKickAhead(PlayerState player, int directionX, int directionY)
    {
        if (directionX == 0 && directionY == 0)
        {
            return false;
        }

        var current = player.Cell;
        var bombCell = new GridPosition(current.X + directionX, current.Y + directionY);
        var bomb = FindBomb(bombCell);
        if (bomb is null)
        {
            return false;
        }

        var target = new GridPosition(bombCell.X + directionX, bombCell.Y + directionY);
        if (!CanBombOccupy(target, bomb))
        {
            return false;
        }

        bomb.Cell = target;
        bomb.MotionRemaining = 0.16;
        bomb.PassThroughPlayers.Add(player.Id);
        return true;
    }

    private bool CanBombOccupy(GridPosition position, BombState? movingBomb = null) =>
        _board[position] == TileType.Floor &&
        !IsBombReserved(position, movingBomb);

    private static bool CircleOverlapsCell(double x, double y, double radius, GridPosition cell)
        => SquaredDistanceToCell(x, y, cell) < radius * radius;

    private static double SquaredDistanceToCell(double x, double y, GridPosition cell)
    {
        var nearestX = Math.Clamp(x, cell.X, cell.X + 1.0);
        var nearestY = Math.Clamp(y, cell.Y, cell.Y + 1.0);
        var deltaX = x - nearestX;
        var deltaY = y - nearestY;
        return (deltaX * deltaX) + (deltaY * deltaY);
    }

    private void UpdateBombPassers()
    {
        foreach (var bomb in _bombs.Where(candidate => !candidate.IsExploded && !candidate.IsAirborne))
        {
            bomb.PassThroughPlayers.RemoveWhere(playerId =>
            {
                var player = _players.FirstOrDefault(candidate => candidate.Id == playerId);
                return player is null || !player.IsAlive || !CircleOverlapsCell(player.X, player.Y, PlayerRadius, bomb.Cell);
            });
        }
    }

    private bool TryPlaceBomb(PlayerState player)
    {
        if (!player.IsAlive || player.ActiveBombs >= player.BombCapacity || _board[player.Cell] != TileType.Floor ||
            IsBombReserved(player.Cell))
        {
            return false;
        }

        var isMega = player.MegaCharges > 0;
        var isCluster = player.ClusterCharges > 0;
        if (isMega)
        {
            player.MegaCharges--;
        }

        if (isCluster)
        {
            player.ClusterCharges--;
        }

        AddBomb(
            player,
            player.Cell,
            player.HasRemote ? 8 : _configuration.BombFuseSeconds,
            Math.Min(PlayerCaps.FireRange + 2, player.FireRange + (isMega ? 2 : 0)),
            isMega,
            isCluster,
            player.HasPiercingFlames,
            countStatistic: true);
        return true;
    }

    private BombState AddBomb(
        PlayerState owner,
        GridPosition cell,
        double fuse,
        int range,
        bool isMega,
        bool isCluster,
        bool isPiercing,
        bool countStatistic)
    {
        var bomb = new BombState
        {
            Id = ++_nextBombId,
            OwnerPlayerId = owner.Id,
            Cell = cell,
            Fuse = fuse,
            InitialFuse = Math.Max(fuse, 0.001),
            Range = Math.Clamp(range, 1, PlayerCaps.FireRange + 2),
            IsMega = isMega,
            IsCluster = isCluster,
            IsPiercing = isPiercing,
            IsBrickDisguised = owner.HasBrickDisguise
        };
        foreach (var player in _players.Where(candidate => candidate.IsAlive &&
                     CircleOverlapsCell(candidate.X, candidate.Y, PlayerRadius, cell)))
        {
            bomb.PassThroughPlayers.Add(player.Id);
        }

        _bombs.Add(bomb);
        owner.ActiveBombs++;
        if (countStatistic)
        {
            owner.Statistics.BombsPlaced++;
        }

        return bomb;
    }

    private bool UseAction(PlayerState player)
    {
        if (player.HasRemote)
        {
            var oldest = _bombs
                .Where(bomb => !bomb.IsExploded && !bomb.IsGhost && !bomb.IsAirborne && bomb.OwnerPlayerId == player.Id)
                .OrderBy(bomb => bomb.Id)
                .FirstOrDefault();
            if (oldest is not null)
            {
                oldest.Fuse = 0;
                return true;
            }
        }

        if (player.HasGlove)
        {
            var adjacent = player.Cell + new GridOffset(player.FacingX, player.FacingY);
            var bomb = FindBomb(adjacent);
            if (bomb is not null)
            {
                var destination = adjacent;
                for (var distance = 0; distance < 3; distance++)
                {
                    var candidate = destination + new GridOffset(player.FacingX, player.FacingY);
                    if (!CanBombOccupy(candidate, bomb))
                    {
                        break;
                    }

                    destination = candidate;
                }

                if (destination != adjacent)
                {
                    bomb.Cell = destination;
                    bomb.MotionRemaining = 0.24;
                    bomb.PassThroughPlayers.Clear();
                    return true;
                }
            }
        }

        if (player.DashCharges > 0 && player.DashTime <= 0)
        {
            player.DashCharges--;
            player.DashTime = 0.34;
            return true;
        }

        return false;
    }

    private void UpdateBombs(double deltaSeconds)
    {
        foreach (var bomb in _bombs.Where(candidate => !candidate.IsExploded))
        {
            if (bomb.IsAirborne)
            {
                bomb.AirborneElapsed += deltaSeconds;
                if (!bomb.IsAirborne)
                {
                    LandGhostBomb(bomb);
                }

                continue;
            }

            bomb.Fuse -= deltaSeconds;
            bomb.MotionRemaining = Math.Max(0, bomb.MotionRemaining - deltaSeconds);
        }

        var explosionQueue = new Queue<BombState>(_bombs
            .Where(bomb => !bomb.IsExploded && !bomb.IsAirborne && bomb.Fuse <= 0)
            .OrderBy(bomb => bomb.Id));
        while (explosionQueue.Count > 0)
        {
            var bomb = explosionQueue.Dequeue();
            if (!bomb.IsExploded)
            {
                ExplodeBomb(bomb, explosionQueue);
            }
        }

        _bombs.RemoveAll(bomb => bomb.IsExploded);
    }

    private BombSnapshot CreateBombSnapshot(BombState bomb)
    {
        if (bomb.IsAirborne)
        {
            return bomb.ToSnapshot([]);
        }

        var projection = ProjectExplosion(bomb);
        var previewCells = projection.PrimaryCells
            .Concat(projection.ClusterCells)
            .Distinct()
            .ToArray();
        return bomb.ToSnapshot(previewCells);
    }

    /// <summary>
    /// Projects the cells the bomb would ignite against the board as it exists now.
    /// The renderer and the explosion itself share this projection so the on-board
    /// warning never promises fire through a blocker that the simulation would stop at.
    /// </summary>
    private BombExplosionProjection ProjectExplosion(BombState bomb) =>
        ProjectExplosion(bomb.Cell, bomb.Range, bomb.IsPiercing, bomb.IsCluster);

    private BombExplosionProjection ProjectExplosion(
        GridPosition origin,
        int range,
        bool isPiercing,
        bool isCluster)
    {
        var primaryCells = new List<GridPosition> { origin };
        var endpoints = new List<GridPosition>(ArenaRules.CardinalDirections.Length);
        foreach (var direction in ArenaRules.CardinalDirections)
        {
            var lastCell = origin;
            var piercedCrate = false;
            for (var distance = 1; distance <= range; distance++)
            {
                var cell = new GridPosition(
                    origin.X + (direction.X * distance),
                    origin.Y + (direction.Y * distance));
                var tile = _board[cell];
                if (tile == TileType.SolidWall)
                {
                    break;
                }

                primaryCells.Add(cell);
                lastCell = cell;
                if (tile != TileType.Crate)
                {
                    continue;
                }

                if (!isPiercing || piercedCrate)
                {
                    break;
                }

                piercedCrate = true;
            }

            endpoints.Add(lastCell);
        }

        var clusterCells = new List<GridPosition>();
        if (isCluster)
        {
            foreach (var endpoint in endpoints)
            {
                foreach (var diagonal in ClusterDirections)
                {
                    var cell = endpoint + diagonal;
                    if (_board[cell] == TileType.Floor)
                    {
                        clusterCells.Add(cell);
                    }
                }
            }
        }

        return new(primaryCells, clusterCells);
    }

    private void ExplodeBomb(BombState bomb, Queue<BombState> explosionQueue)
    {
        bomb.IsExploded = true;
        var owner = FindPlayer(bomb.OwnerPlayerId);
        if (owner is not null)
        {
            if (bomb.IsGhost)
            {
                if (owner.ActiveGhostBombId == bomb.Id)
                {
                    owner.ActiveGhostBombId = null;
                }
            }
            else
            {
                owner.ActiveBombs = Math.Max(0, owner.ActiveBombs - 1);
            }
        }

        var projection = ProjectExplosion(bomb);
        foreach (var cell in projection.PrimaryCells)
        {
            AddFlame(cell, bomb, explosionQueue);
            if (_board[cell] == TileType.Crate)
            {
                _board[cell] = TileType.Floor;
                InvalidateBoardSnapshot();
                if (owner is not null)
                {
                    owner.Statistics.CratesDestroyed++;
                }

                TryDropItem(cell);
            }
        }

        foreach (var cell in projection.ClusterCells)
        {
            AddFlame(cell, bomb, explosionQueue, 0.65);
        }
    }

    private void AddFlame(
        GridPosition cell,
        BombState source,
        Queue<BombState> explosionQueue,
        double lifetimeMultiplier = 1)
    {
        var lifetime = _configuration.FlameLifetimeSeconds * lifetimeMultiplier;
        var existing = _flames.FirstOrDefault(flame => flame.Cell == cell && flame.SourceBombId == source.Id);
        if (existing is not null)
        {
            existing.Remaining = Math.Max(existing.Remaining, lifetime);
        }
        else
        {
            _flames.Add(new FlameState
            {
                Cell = cell,
                Remaining = lifetime,
                SourcePlayerId = source.OwnerPlayerId,
                SourceBombId = source.Id,
                IsMega = source.IsMega,
                IsGhostSource = source.IsGhost,
                SourceGhostGeneration = source.SourceGhostGeneration
            });
        }

        var chainedBomb = FindBomb(cell);
        if (chainedBomb is not null && chainedBomb != source && !chainedBomb.IsExploded)
        {
            chainedBomb.Fuse = 0;
            explosionQueue.Enqueue(chainedBomb);
        }
    }

    private void UpdateFlames(double deltaSeconds)
    {
        foreach (var flame in _flames)
        {
            flame.Remaining -= deltaSeconds;
        }

        var ghostKillCandidates = new List<(PlayerState Victim, FlameState Source, double X, double Y)>();
        foreach (var flame in _flames.Where(candidate => candidate.Remaining > 0))
        {
            foreach (var player in _players.Where(candidate => candidate.IsAlive && candidate.Cell == flame.Cell).ToArray())
            {
                var deathX = player.X;
                var deathY = player.Y;
                if (HurtPlayer(player, flame) && flame.IsGhostSource)
                {
                    ghostKillCandidates.Add((player, flame, deathX, deathY));
                }
            }
        }

        if (_players.Count(player => player.IsAlive) > 1)
        {
            foreach (var candidate in ghostKillCandidates)
            {
                TryResolveGhostRevival(candidate.Victim, candidate.Source, candidate.X, candidate.Y);
            }
        }

        _flames.RemoveAll(flame => flame.Remaining <= 0);
    }

    private bool HurtPlayer(PlayerState player, FlameState flame)
    {
        if (!player.IsAlive || player.Invulnerability > 0 || player.IsFlameproof)
        {
            return false;
        }

        if (player.Shield > 0)
        {
            player.Shield--;
            player.Invulnerability = 0.65;
            return false;
        }

        player.Health--;
        if (player.Health > 0)
        {
            player.Invulnerability = 0.90;
            return false;
        }

        var deathX = player.X;
        var deathY = player.Y;
        BecomeGhost(player, deathX, deathY);
        player.Statistics.Deaths++;
        var attacker = FindPlayer(flame.SourcePlayerId);
        if (attacker is not null && attacker.Id != player.Id)
        {
            attacker.Statistics.Eliminations++;
        }

        return true;
    }

    private void TryDropItem(GridPosition cell)
    {
        if (!_random.Chance(_configuration.ItemDropChance))
        {
            return;
        }

        _items.Add(new ItemState
        {
            Id = ++_nextItemId,
            X = cell.X + 0.5,
            Y = cell.Y + 0.5,
            Definition = PowerUpCatalog.Select(_random)
        });
    }

    private void UpdateItems(double deltaSeconds)
    {
        for (var index = _items.Count - 1; index >= 0; index--)
        {
            var item = _items[index];
            item.Remaining -= deltaSeconds;
            if (item.Remaining <= 0)
            {
                _items.RemoveAt(index);
                continue;
            }

            var magnet = _players
                .Where(player => player.IsAlive && player.HasMagnet)
                .Select(player => new
                {
                    Player = player,
                    Distance = Distance(player.X, player.Y, item.X, item.Y)
                })
                .Where(candidate => candidate.Distance < MagnetAttractionRadius)
                .OrderBy(candidate => candidate.Distance)
                .ThenBy(candidate => candidate.Player.Id)
                .FirstOrDefault();
            if (magnet is not null && magnet.Distance > 0.001)
            {
                var travel = Math.Min(magnet.Distance, 3.5 * deltaSeconds);
                item.X += ((magnet.Player.X - item.X) / magnet.Distance) * travel;
                item.Y += ((magnet.Player.Y - item.Y) / magnet.Distance) * travel;
            }

            var collector = _players
                .Where(player => player.IsAlive && Distance(player.X, player.Y, item.X, item.Y) < 0.52)
                .OrderBy(player => player.Id)
                .FirstOrDefault();
            if (collector is null)
            {
                continue;
            }

            ApplyPowerUp(collector, item.Definition.Kind);
            collector.Statistics.ItemsCollected++;
            _items.RemoveAt(index);
        }
    }

    private void ApplyPowerUp(PlayerState player, PowerUpKind kind)
    {
        switch (kind)
        {
            case PowerUpKind.BombCapacity:
                player.BombCapacity = Math.Min(PlayerCaps.BombCapacity, player.BombCapacity + 1);
                break;
            case PowerUpKind.FireRange:
                player.FireRange = Math.Min(PlayerCaps.FireRange, player.FireRange + 1);
                break;
            case PowerUpKind.Speed:
                player.MoveSpeed = Math.Min(PlayerCaps.MoveSpeed, player.MoveSpeed + 0.34);
                break;
            case PowerUpKind.Kick:
                player.CanKick = true;
                break;
            case PowerUpKind.Glove:
                player.HasGlove = true;
                break;
            case PowerUpKind.Remote:
                player.HasRemote = true;
                break;
            case PowerUpKind.Pierce:
                player.HasPiercingFlames = true;
                break;
            case PowerUpKind.BombPass:
                player.CanPassBombs = true;
                break;
            case PowerUpKind.WallPass:
                player.CanPassCrates = true;
                break;
            case PowerUpKind.FlamePass:
                player.IsFlameproof = true;
                break;
            case PowerUpKind.Shield:
                player.Shield = Math.Min(PlayerCaps.Shield, player.Shield + 1);
                break;
            case PowerUpKind.Heart:
                player.Health = Math.Min(PlayerCaps.Health, player.Health + 1);
                break;
            case PowerUpKind.Dash:
                player.DashCharges = Math.Min(PlayerCaps.DashCharges, player.DashCharges + 2);
                break;
            case PowerUpKind.Mega:
                player.MegaCharges = Math.Min(PlayerCaps.MegaCharges, player.MegaCharges + 1);
                break;
            case PowerUpKind.Cluster:
                player.ClusterCharges = Math.Min(PlayerCaps.ClusterCharges, player.ClusterCharges + 1);
                break;
            case PowerUpKind.Freeze:
                foreach (var opponent in _players.Where(candidate => candidate.IsAlive && candidate.Id != player.Id))
                {
                    opponent.Frozen = Math.Max(opponent.Frozen, 2.20);
                }

                break;
            case PowerUpKind.Magnet:
                player.HasMagnet = true;
                break;
            case PowerUpKind.BrickDisguise:
                player.HasBrickDisguise = true;
                break;
            case PowerUpKind.Mystery:
                ApplyMysteryPower(player);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    private void ApplyMysteryPower(PlayerState player)
    {
        var roll = _random.NextDouble();
        if (roll < 0.20)
        {
            player.Reversed = Math.Max(player.Reversed, 6);
        }
        else if (roll < 0.38)
        {
            player.Slowed = Math.Max(player.Slowed, 6);
        }
        else if (roll < 0.55)
        {
            TeleportPlayer(player);
        }
        else if (roll < 0.72)
        {
            player.Shield = Math.Min(PlayerCaps.Shield, player.Shield + 2);
        }
        else if (roll < 0.88)
        {
            player.MegaCharges = Math.Min(PlayerCaps.MegaCharges, player.MegaCharges + 2);
            player.ClusterCharges = Math.Min(PlayerCaps.ClusterCharges, player.ClusterCharges + 1);
        }
        else
        {
            player.BombCapacity = Math.Min(PlayerCaps.BombCapacity, player.BombCapacity + 2);
            player.FireRange = Math.Min(PlayerCaps.FireRange, player.FireRange + 2);
            player.MoveSpeed = Math.Min(PlayerCaps.MoveSpeed, player.MoveSpeed + 0.50);
        }
    }

    private void TeleportPlayer(PlayerState player)
    {
        var candidates = new List<GridPosition>();
        for (var y = 1; y < ArenaRules.Height - 1; y++)
        {
            for (var x = 1; x < ArenaRules.Width - 1; x++)
            {
                var cell = new GridPosition(x, y);
                if (_board[cell] == TileType.Floor && FindBomb(cell) is null)
                {
                    candidates.Add(cell);
                }
            }
        }

        if (candidates.Count > 0)
        {
            var destination = candidates[_random.NextInt(candidates.Count)];
            player.X = destination.X + 0.5;
            player.Y = destination.Y + 0.5;
            player.Invulnerability = Math.Max(player.Invulnerability, 1);
        }
    }

    private void EvaluateRoundEnd()
    {
        var survivors = _players.Where(player => player.IsAlive).ToArray();
        if (survivors.Length > 1)
        {
            return;
        }

        PlayerState? winner = null;
        if (survivors.Length == 1)
        {
            winner = survivors[0];
            winner.Crowns++;
            winner.Statistics.RoundsWon++;
            if (winner.Crowns >= _configuration.TargetCrowns)
            {
                MatchWinnerPlayerId = winner.Id;
            }
        }

        LastRound = new RoundResultSnapshot(
            RoundNumber,
            winner?.Id,
            winner is null,
            MatchWinnerPlayerId);
        Phase = MatchWinnerPlayerId is null ? GamePhase.RoundOver : GamePhase.MatchOver;
        _accumulator = 0;
    }

    private BombState? FindBomb(GridPosition cell) =>
        _bombs.FirstOrDefault(bomb => !bomb.IsExploded && !bomb.IsAirborne && bomb.Cell == cell);

    private PlayerState? FindPlayer(int playerId) =>
        _players.FirstOrDefault(player => player.Id == playerId);

    private static double Distance(double x1, double y1, double x2, double y2)
    {
        var deltaX = x2 - x1;
        var deltaY = y2 - y1;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    private static readonly GridOffset[] ClusterDirections =
    [
        new(1, 1),
        new(-1, 1),
        new(1, -1),
        new(-1, -1)
    ];

    private sealed record BombExplosionProjection(
        IReadOnlyList<GridPosition> PrimaryCells,
        IReadOnlyList<GridPosition> ClusterCells);

    internal void DebugClearCrates()
    {
        _board.ClearDestructibleCells();
        InvalidateBoardSnapshot();
    }

    internal void DebugSetTile(GridPosition cell, TileType tile)
    {
        _board[cell] = tile;
        InvalidateBoardSnapshot();
    }

    internal void DebugSetPlayerPosition(int playerId, GridPosition cell)
    {
        var player = FindPlayer(playerId) ?? throw new ArgumentOutOfRangeException(nameof(playerId));
        player.X = cell.X + 0.5;
        player.Y = cell.Y + 0.5;
    }

    internal void DebugSetPlayerPosition(int playerId, double x, double y)
    {
        var player = FindPlayer(playerId) ?? throw new ArgumentOutOfRangeException(nameof(playerId));
        player.X = x;
        player.Y = y;
    }

    internal double DebugGetAiThinkRemaining(int playerId) =>
        (FindPlayer(playerId) ?? throw new ArgumentOutOfRangeException(nameof(playerId))).AiThinkRemaining;

    internal IReadOnlyList<GridPosition> DebugGetAiRoute(int playerId) =>
        (FindPlayer(playerId) ?? throw new ArgumentOutOfRangeException(nameof(playerId))).AiRoute.ToArray();

    internal void DebugApplyPowerUp(int playerId, PowerUpKind kind)
    {
        var player = FindPlayer(playerId) ?? throw new ArgumentOutOfRangeException(nameof(playerId));
        ApplyPowerUp(player, kind);
    }

    internal long DebugPlaceBomb(
        int playerId,
        GridPosition cell,
        double fuse,
        int range,
        bool isPiercing = false,
        bool isMega = false,
        bool isCluster = false)
    {
        var player = FindPlayer(playerId) ?? throw new ArgumentOutOfRangeException(nameof(playerId));
        if (_board[cell] != TileType.Floor || IsBombReserved(cell))
        {
            throw new InvalidOperationException("A debug bomb requires an empty floor cell.");
        }

        return AddBomb(player, cell, fuse, range, isMega, isCluster, isPiercing, countStatistic: true).Id;
    }

    internal void DebugSpawnItem(GridPosition cell, PowerUpKind kind)
    {
        _items.Add(new ItemState
        {
            Id = ++_nextItemId,
            X = cell.X + 0.5,
            Y = cell.Y + 0.5,
            Definition = PowerUpCatalog.Get(kind)
        });
    }

    internal IReadOnlySet<int> DebugBombPassThroughPlayers(long bombId)
    {
        var bomb = _bombs.FirstOrDefault(candidate => !candidate.IsExploded && candidate.Id == bombId)
            ?? throw new ArgumentOutOfRangeException(nameof(bombId));
        return bomb.PassThroughPlayers.ToHashSet();
    }

    internal void DebugEliminatePlayer(int playerId, int? sourcePlayerId = null)
    {
        var player = FindPlayer(playerId) ?? throw new ArgumentOutOfRangeException(nameof(playerId));
        if (!player.IsAlive)
        {
            return;
        }

        BecomeGhost(player, player.X, player.Y);
        player.Statistics.Deaths++;
        if (sourcePlayerId is { } source && source != playerId)
        {
            var attacker = FindPlayer(source);
            if (attacker is not null)
            {
                attacker.Statistics.Eliminations++;
            }
        }
    }
}
