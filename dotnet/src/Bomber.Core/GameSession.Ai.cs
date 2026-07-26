namespace Bomber.Core;

public sealed partial class GameSession
{
    private const double AiTargetTolerance = 0.075;

    private void UpdateAi(PlayerState player)
    {
        if (player.AiRoute.Count > 0 && !IsAiCellWalkable(player, player.AiRoute[0]))
        {
            player.AiRoute.Clear();
            player.AiThinkRemaining = 0;
        }

        // An idle or temporarily trapped AI used to ignore its cooldown whenever it had no
        // route, causing a full danger-map/pathfinding pass on every 60 Hz simulation step.
        // Honor the cooldown in every state; invalidated routes explicitly reset it above.
        if (player.AiThinkRemaining > 0)
        {
            return;
        }

        var (thinkInterval, escapeLead, safetyMargin) = player.Difficulty switch
        {
            AiDifficulty.Novice => (0.42, 1.20, 0.06),
            AiDifficulty.Expert => (0.10, 2.70, 0.24),
            _ => (0.21, 1.95, 0.14)
        };
        player.AiThinkRemaining = thinkInterval;

        var danger = BuildDangerMap();
        if (danger.TryGetValue(player.Cell, out var dangerTime) && dangerTime <= escapeLead)
        {
            var escape = FindPath(
                player,
                (cell, depth) => depth > 0 &&
                    (!danger.TryGetValue(cell, out var arrivalDanger) || arrivalDanger > (depth / player.MoveSpeed) + 0.55),
                danger,
                safetyMargin,
                26);
            if (escape is not null)
            {
                SetAiRoute(player, escape, "Escaping blast");
                if (player.DashCharges > 0 && dangerTime < 0.72)
                {
                    player.AiActionRequested = true;
                }

                return;
            }

            player.AiRoute.Clear();
            player.AiIntent = "Sheltering";
            return;
        }

        if (TryPlanRemoteDetonation(player, danger))
        {
            return;
        }

        if (CanAiPlantBomb(player) && BombHasPurpose(player))
        {
            var escape = FindEscapeAfterPlanting(player, danger, safetyMargin);
            if (escape is not null)
            {
                SetAiRoute(player, escape, "Bombing and escaping");
                player.AiBombRequested = true;
                return;
            }
        }

        var itemCells = _items.Select(item => item.Cell).ToHashSet();
        if (itemCells.Count > 0)
        {
            var itemRoute = FindPath(player, (cell, _) => itemCells.Contains(cell), danger, safetyMargin, 34);
            if (itemRoute is not null)
            {
                SetAiRoute(player, itemRoute, "Collecting chip");
                return;
            }
        }

        var crateRoute = FindPath(
            player,
            (cell, _) => ArenaRules.CardinalDirections.Any(direction => _board[cell + direction] == TileType.Crate),
            danger,
            safetyMargin,
            38);
        if (crateRoute is not null)
        {
            if (crateRoute.Count == 0 && CanAiPlantBomb(player))
            {
                var escape = FindEscapeAfterPlanting(player, danger, safetyMargin);
                if (escape is not null)
                {
                    SetAiRoute(player, escape, "Clearing crates");
                    player.AiBombRequested = true;
                    return;
                }
            }

            SetAiRoute(player, crateRoute, "Hunting crates");
            return;
        }

        var opponentCells = _players
            .Where(candidate => candidate.IsAlive && candidate.Id != player.Id)
            .Select(candidate => candidate.Cell)
            .ToArray();
        var attackRoute = FindPath(
            player,
            // A same-cell rival is not an attack position: returning an empty route there lets
            // overlapping AIs regard one another as reached and remain idle forever. Move one
            // cell apart first; from an adjacent cell BombHasPurpose can plan the attack.
            (cell, _) => opponentCells.Any(opponent => ManhattanDistance(cell, opponent) == 1),
            danger,
            safetyMargin,
            40);
        if (attackRoute is not null)
        {
            SetAiRoute(player, attackRoute, "Tracking rival");
            return;
        }

        var safeNeighbors = OrderedDirections(player.Id)
            .Select(direction => player.Cell + direction)
            .Where(cell => IsAiCellWalkable(player, cell) && !danger.ContainsKey(cell))
            .ToArray();
        player.AiRoute.Clear();
        if (safeNeighbors.Length > 0)
        {
            player.AiRoute.Add(safeNeighbors[_random.NextInt(safeNeighbors.Length)]);
            player.AiIntent = "Patrolling";
        }
        else
        {
            player.AiIntent = "Holding position";
        }
    }

    private (double Horizontal, double Vertical) GetAiMovement(PlayerState player)
    {
        while (player.AiRoute.Count > 0)
        {
            var target = player.AiRoute[0];
            var targetX = target.X + 0.5;
            var targetY = target.Y + 0.5;
            var deltaX = targetX - player.X;
            var deltaY = targetY - player.Y;
            if (IsAtAiTarget(player, target))
            {
                player.X = targetX;
                player.Y = targetY;
                player.AiRoute.RemoveAt(0);
                continue;
            }

            if (!IsAiCellWalkable(player, target))
            {
                player.AiRoute.Clear();
                player.AiThinkRemaining = 0;
                player.AiIntent = "Replanning";
                return default;
            }

            return (deltaX, deltaY);
        }

        return default;
    }

    private bool TryPlanRemoteDetonation(PlayerState player, IReadOnlyDictionary<GridPosition, double> danger)
    {
        if (!player.HasRemote)
        {
            return false;
        }

        var bomb = _bombs
            .Where(candidate => !candidate.IsExploded && candidate.OwnerPlayerId == player.Id)
            .OrderBy(candidate => candidate.Id)
            .FirstOrDefault();
        if (bomb is null)
        {
            return false;
        }

        var projection = ProjectExplosion(bomb);
        var blast = projection.PrimaryCells.Concat(projection.ClusterCells).ToHashSet();
        var catchesOpponent = _players.Any(candidate => candidate.IsAlive && candidate.Id != player.Id && blast.Contains(candidate.Cell));
        if (!catchesOpponent || blast.Contains(player.Cell) || danger.TryGetValue(player.Cell, out var dangerTime) && dangerTime < 0.45)
        {
            return false;
        }

        player.AiActionRequested = true;
        player.AiIntent = "Remote detonation";
        return true;
    }

    private bool CanAiPlantBomb(PlayerState player) =>
        player.IsAlive &&
        player.ActiveBombs < player.BombCapacity &&
        !IsBombReserved(player.Cell) &&
        _board[player.Cell] == TileType.Floor &&
        Math.Abs(player.X - (player.Cell.X + 0.5)) < 0.14 &&
        Math.Abs(player.Y - (player.Cell.Y + 0.5)) < 0.14;

    private bool BombHasPurpose(PlayerState player)
    {
        foreach (var direction in ArenaRules.CardinalDirections)
        {
            for (var distance = 1; distance <= player.FireRange; distance++)
            {
                var cell = new GridPosition(
                    player.Cell.X + (direction.X * distance),
                    player.Cell.Y + (direction.Y * distance));
                var tile = _board[cell];
                if (tile == TileType.SolidWall)
                {
                    break;
                }

                if (_players.Any(candidate => candidate.IsAlive && candidate.Id != player.Id && candidate.Cell == cell))
                {
                    return true;
                }

                if (tile == TileType.Crate)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private List<GridPosition>? FindEscapeAfterPlanting(
        PlayerState player,
        IReadOnlyDictionary<GridPosition, double> existingDanger,
        double safetyMargin)
    {
        var combinedDanger = new Dictionary<GridPosition, double>(existingDanger);
        var fuse = player.HasRemote ? 8 : _configuration.BombFuseSeconds;
        var range = Math.Min(PlayerCaps.FireRange + 2, player.FireRange + (player.MegaCharges > 0 ? 2 : 0));
        var projection = ProjectExplosion(
            player.Cell,
            range,
            player.HasPiercingFlames,
            player.ClusterCharges > 0);
        var virtualBlast = projection.PrimaryCells.Concat(projection.ClusterCells).ToHashSet();
        foreach (var cell in virtualBlast)
        {
            if (!combinedDanger.TryGetValue(cell, out var oldTime) || fuse < oldTime)
            {
                combinedDanger[cell] = fuse;
            }
        }

        return FindPath(
            player,
            (cell, depth) => depth > 0 && !virtualBlast.Contains(cell) &&
                (!combinedDanger.TryGetValue(cell, out var time) || time > (depth / player.MoveSpeed) + 0.60),
            combinedDanger,
            safetyMargin,
            30,
            player.Cell);
    }

    private Dictionary<GridPosition, double> BuildDangerMap()
    {
        var danger = new Dictionary<GridPosition, double>();
        foreach (var flame in _flames)
        {
            danger[flame.Cell] = 0;
        }

        foreach (var bomb in _bombs.Where(candidate => !candidate.IsExploded))
        {
            var flightRemaining = bomb.IsAirborne ? bomb.AirborneDuration - bomb.AirborneElapsed : 0;
            var time = Math.Max(0, bomb.Fuse + flightRemaining);
            var projection = ProjectExplosion(bomb);
            foreach (var cell in projection.PrimaryCells.Concat(projection.ClusterCells))
            {
                if (!danger.TryGetValue(cell, out var oldTime) || time < oldTime)
                {
                    danger[cell] = time;
                }
            }
        }

        return danger;
    }

    private List<GridPosition>? FindPath(
        PlayerState player,
        Func<GridPosition, int, bool> isGoal,
        IReadOnlyDictionary<GridPosition, double> danger,
        double safetyMargin,
        int maximumDepth,
        GridPosition? virtualBombCell = null)
    {
        var start = player.Cell;
        if (isGoal(start, 0))
        {
            // Entering a cell changes PlayerState.Cell before the player reaches its center. A route
            // that is empty at that point leaves the AI unable to perform center-gated actions such
            // as planting a bomb. Keep the final centering leg as part of the path contract.
            return IsAtAiTarget(player, start) ? [] : [start];
        }

        var queue = new Queue<GridPosition>();
        var depth = new Dictionary<GridPosition, int> { [start] = 0 };
        var previous = new Dictionary<GridPosition, GridPosition>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var nextDepth = depth[current] + 1;
            if (nextDepth > maximumDepth)
            {
                continue;
            }

            foreach (var direction in OrderedDirections(player.Id + depth[current]))
            {
                var next = current + direction;
                if (depth.ContainsKey(next) || !IsAiCellWalkable(player, next, virtualBombCell))
                {
                    continue;
                }

                var arrival = nextDepth / Math.Max(0.1, player.MoveSpeed);
                if (danger.TryGetValue(next, out var dangerTime) && dangerTime <= arrival + safetyMargin)
                {
                    continue;
                }

                depth[next] = nextDepth;
                previous[next] = current;
                if (isGoal(next, nextDepth))
                {
                    return ReconstructPath(start, next, previous);
                }

                queue.Enqueue(next);
            }
        }

        return null;
    }

    private bool IsAiCellWalkable(PlayerState player, GridPosition cell, GridPosition? virtualBombCell = null)
    {
        var tile = _board[cell];
        if (tile == TileType.SolidWall || tile == TileType.Crate && !player.CanPassCrates)
        {
            return false;
        }

        if (virtualBombCell == cell)
        {
            return false;
        }

        var bomb = FindBomb(cell);
        return bomb is null || player.CanPassBombs || bomb.PassThroughPlayers.Contains(player.Id);
    }

    private static List<GridPosition> ReconstructPath(
        GridPosition start,
        GridPosition destination,
        IReadOnlyDictionary<GridPosition, GridPosition> previous)
    {
        var path = new List<GridPosition>();
        var current = destination;
        while (current != start)
        {
            path.Add(current);
            current = previous[current];
        }

        path.Reverse();
        return path;
    }

    private static IEnumerable<GridOffset> OrderedDirections(int rotation)
    {
        for (var index = 0; index < ArenaRules.CardinalDirections.Length; index++)
        {
            yield return ArenaRules.CardinalDirections[(index + rotation) % ArenaRules.CardinalDirections.Length];
        }
    }

    private static int ManhattanDistance(GridPosition first, GridPosition second) =>
        Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);

    private static bool IsAtAiTarget(PlayerState player, GridPosition target)
    {
        var deltaX = (target.X + 0.5) - player.X;
        var deltaY = (target.Y + 0.5) - player.Y;
        return (deltaX * deltaX) + (deltaY * deltaY) < AiTargetTolerance * AiTargetTolerance;
    }

    private static void SetAiRoute(PlayerState player, IEnumerable<GridPosition> route, string intent)
    {
        player.AiRoute.Clear();
        player.AiRoute.AddRange(route);
        player.AiIntent = intent;
    }
}
