namespace Bomber.Core;

internal static class ArenaRules
{
    public const int Width = 17;
    public const int Height = 13;

    public static readonly GridOffset[] CardinalDirections =
    [
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1)
    ];

    public static readonly GridPosition[] SpawnCells =
    [
        new(1, 1),
        new(Width - 2, Height - 2),
        new(Width - 2, 1),
        new(1, Height - 2)
    ];

    public static HashSet<GridPosition> CreateSpawnSafeCells()
    {
        var cells = new HashSet<GridPosition>();
        foreach (var spawn in SpawnCells)
        {
            cells.Add(spawn);
            cells.Add(new(spawn.X == 1 ? 2 : Width - 3, spawn.Y));
            cells.Add(new(spawn.X, spawn.Y == 1 ? 2 : Height - 3));
        }

        return cells;
    }
}

internal sealed class ArenaBoard
{
    private readonly TileType[] _tiles = new TileType[ArenaRules.Width * ArenaRules.Height];

    public TileType this[int x, int y]
    {
        get => IsInside(x, y) ? _tiles[(y * ArenaRules.Width) + x] : TileType.SolidWall;
        set
        {
            if (!IsInside(x, y))
            {
                throw new ArgumentOutOfRangeException($"Cell ({x}, {y}) is outside the arena.");
            }

            _tiles[(y * ArenaRules.Width) + x] = value;
        }
    }

    public TileType this[GridPosition position]
    {
        get => this[position.X, position.Y];
        set => this[position.X, position.Y] = value;
    }

    public static bool IsInside(int x, int y) =>
        x >= 0 && x < ArenaRules.Width && y >= 0 && y < ArenaRules.Height;

    public void Generate(DeterministicRandom random, double crateDensity)
    {
        var safeCells = ArenaRules.CreateSpawnSafeCells();
        for (var y = 0; y < ArenaRules.Height; y++)
        {
            for (var x = 0; x < ArenaRules.Width; x++)
            {
                var position = new GridPosition(x, y);
                if (x == 0 || y == 0 || x == ArenaRules.Width - 1 || y == ArenaRules.Height - 1 ||
                    (x % 2 == 0 && y % 2 == 0))
                {
                    this[position] = TileType.SolidWall;
                }
                else if (!safeCells.Contains(position) && random.Chance(crateDensity))
                {
                    this[position] = TileType.Crate;
                }
                else
                {
                    this[position] = TileType.Floor;
                }
            }
        }
    }

    public void ClearDestructibleCells()
    {
        for (var y = 0; y < ArenaRules.Height; y++)
        {
            for (var x = 0; x < ArenaRules.Width; x++)
            {
                if (this[x, y] == TileType.Crate)
                {
                    this[x, y] = TileType.Floor;
                }
            }
        }
    }

    public BoardSnapshot ToSnapshot() =>
        new(ArenaRules.Width, ArenaRules.Height, (TileType[])_tiles.Clone());
}

internal sealed class MutablePlayerStatistics
{
    public int BombsPlaced { get; set; }
    public int CratesDestroyed { get; set; }
    public int ItemsCollected { get; set; }
    public int Eliminations { get; set; }
    public int Deaths { get; set; }
    public int GhostBombsThrown { get; set; }
    public int Revivals { get; set; }
    public int RoundsWon { get; set; }

    public PlayerStatisticsSnapshot ToSnapshot() =>
        new(BombsPlaced, CratesDestroyed, ItemsCollected, Eliminations, Deaths, GhostBombsThrown, Revivals, RoundsWon);
}

internal sealed class PlayerState
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Color { get; init; } = string.Empty;
    public PlayerKind Kind { get; init; }
    public AiDifficulty Difficulty { get; init; }
    public MutablePlayerStatistics Statistics { get; } = new();
    public int Crowns { get; set; }

    public double X { get; set; }
    public double Y { get; set; }
    public int FacingX { get; set; }
    public int FacingY { get; set; } = 1;
    public int RecentMoveX { get; set; }
    public int RecentMoveY { get; set; }
    public double MovementIdleSeconds { get; set; } = double.PositiveInfinity;
    public int BufferedTurnX { get; set; }
    public int BufferedTurnY { get; set; }
    public int BufferedForwardX { get; set; }
    public int BufferedForwardY { get; set; }
    public double BufferedTurnTarget { get; set; }
    public bool IsAlive { get; set; }
    public bool IsGhost { get; set; }
    public int GhostGeneration { get; set; }
    public double GhostTrack { get; set; }
    public long? ActiveGhostBombId { get; set; }
    public double GhostThinkRemaining { get; set; }
    public int? GhostTargetPlayerId { get; set; }
    public double GhostAimTrack { get; set; }
    public int GhostAimFacingX { get; set; }
    public int GhostAimFacingY { get; set; }
    public bool HasGhostAim { get; set; }
    public int Health { get; set; }
    public int Shield { get; set; }
    public double Invulnerability { get; set; }
    public double Frozen { get; set; }
    public double Reversed { get; set; }
    public double Slowed { get; set; }
    public double DashTime { get; set; }

    public int BombCapacity { get; set; }
    public int ActiveBombs { get; set; }
    public int FireRange { get; set; }
    public double MoveSpeed { get; set; }
    public bool CanKick { get; set; }
    public bool HasGlove { get; set; }
    public bool HasRemote { get; set; }
    public bool HasPiercingFlames { get; set; }
    public bool CanPassBombs { get; set; }
    public bool CanPassCrates { get; set; }
    public bool IsFlameproof { get; set; }
    public bool HasMagnet { get; set; }
    public bool HasBrickDisguise { get; set; }
    public int DashCharges { get; set; }
    public int MegaCharges { get; set; }
    public int ClusterCharges { get; set; }

    public PlayerControls Controls { get; set; }
    public bool BombButtonHeld { get; set; }
    public bool ActionButtonHeld { get; set; }
    public bool BombRequested { get; set; }
    public bool ActionRequested { get; set; }

    public double AiThinkRemaining { get; set; }
    public List<GridPosition> AiRoute { get; } = [];
    public string AiIntent { get; set; } = "Idle";
    public bool AiBombRequested { get; set; }
    public bool AiActionRequested { get; set; }

    public GridPosition Cell => new((int)Math.Floor(X), (int)Math.Floor(Y));

    public void ResetForRound(GridPosition spawn, double spawnProtection)
    {
        X = spawn.X + 0.5;
        Y = spawn.Y + 0.5;
        FacingX = 0;
        FacingY = spawn.Y == 1 ? 1 : -1;
        RecentMoveX = 0;
        RecentMoveY = 0;
        MovementIdleSeconds = double.PositiveInfinity;
        ClearBufferedTurn();
        IsAlive = true;
        IsGhost = false;
        GhostGeneration = 0;
        GhostTrack = 0;
        ActiveGhostBombId = null;
        GhostThinkRemaining = 0;
        GhostTargetPlayerId = null;
        GhostAimTrack = 0;
        GhostAimFacingX = 0;
        GhostAimFacingY = 0;
        HasGhostAim = false;
        Health = 1;
        Shield = 0;
        Invulnerability = spawnProtection;
        Frozen = 0;
        Reversed = 0;
        Slowed = 0;
        DashTime = 0;
        BombCapacity = 1;
        ActiveBombs = 0;
        FireRange = 1;
        MoveSpeed = 3.15;
        CanKick = false;
        HasGlove = false;
        HasRemote = false;
        HasPiercingFlames = false;
        CanPassBombs = false;
        CanPassCrates = false;
        IsFlameproof = false;
        HasMagnet = false;
        HasBrickDisguise = false;
        DashCharges = 0;
        MegaCharges = 0;
        ClusterCharges = 0;
        Controls = default;
        BombButtonHeld = false;
        ActionButtonHeld = false;
        BombRequested = false;
        ActionRequested = false;
        AiThinkRemaining = 0;
        AiRoute.Clear();
        AiIntent = "Scanning";
        AiBombRequested = false;
        AiActionRequested = false;
    }

    public void ClearBufferedTurn()
    {
        BufferedTurnX = 0;
        BufferedTurnY = 0;
        BufferedForwardX = 0;
        BufferedForwardY = 0;
        BufferedTurnTarget = 0;
    }

    public PlayerSnapshot ToSnapshot() =>
        new(
            Id,
            Name,
            Color,
            Kind,
            Difficulty,
            X,
            Y,
            FacingX,
            FacingY,
            IsAlive,
            IsGhost,
            Crowns,
            Health,
            Shield,
            Invulnerability,
            Frozen,
            BombCapacity,
            ActiveBombs,
            FireRange,
            MoveSpeed,
            CanKick,
            HasGlove,
            HasRemote,
            HasPiercingFlames,
            CanPassBombs,
            CanPassCrates,
            IsFlameproof,
            HasMagnet,
            HasBrickDisguise,
            DashCharges,
            MegaCharges,
            ClusterCharges,
            IsGhost && ActiveGhostBombId is null,
            AiIntent,
            Statistics.ToSnapshot());
}

internal sealed class BombState
{
    public long Id { get; init; }
    public int OwnerPlayerId { get; init; }
    public GridPosition Cell { get; set; }
    public double Fuse { get; set; }
    public double InitialFuse { get; init; }
    public int Range { get; init; }
    public bool IsMega { get; init; }
    public bool IsCluster { get; init; }
    public bool IsPiercing { get; init; }
    public bool IsBrickDisguised { get; init; }
    public bool IsGhost { get; init; }
    public int SourceGhostGeneration { get; init; }
    public bool IsExploded { get; set; }
    public double MotionRemaining { get; set; }
    public double AirborneElapsed { get; set; }
    public double AirborneDuration { get; init; }
    public double AirborneFromX { get; init; }
    public double AirborneFromY { get; init; }
    public HashSet<int> PassThroughPlayers { get; } = [];
    public List<GridPosition> GhostLandingCandidates { get; } = [];

    public bool IsAirborne => AirborneDuration > 0 && AirborneElapsed < AirborneDuration;
    public double AirborneProgress => AirborneDuration <= 0 ? 1 : Math.Clamp(AirborneElapsed / AirborneDuration, 0, 1);
    private double AirborneEase => AirborneProgress * AirborneProgress * (3 - (2 * AirborneProgress));
    public double X => IsAirborne ? AirborneFromX + (((Cell.X + 0.5) - AirborneFromX) * AirborneEase) : Cell.X + 0.5;
    public double Y => IsAirborne ? AirborneFromY + (((Cell.Y + 0.5) - AirborneFromY) * AirborneEase) : Cell.Y + 0.5;

    public BombSnapshot ToSnapshot(IReadOnlyList<GridPosition> flamePreviewCells) =>
        new(
            Id,
            OwnerPlayerId,
            Cell,
            X,
            Y,
            Math.Max(0, Fuse),
            InitialFuse,
            Range,
            IsMega,
            IsCluster,
            IsPiercing,
            IsBrickDisguised,
            MotionRemaining > 0,
            IsGhost,
            IsAirborne,
            AirborneProgress,
            flamePreviewCells);
}

internal sealed class FlameState
{
    public GridPosition Cell { get; init; }
    public double Remaining { get; set; }
    public int SourcePlayerId { get; set; }
    public long SourceBombId { get; set; }
    public bool IsMega { get; set; }
    public bool IsGhostSource { get; set; }
    public int SourceGhostGeneration { get; set; }

    public FlameSnapshot ToSnapshot() =>
        new(Cell, Math.Max(0, Remaining), SourcePlayerId, SourceBombId, IsMega, IsGhostSource, SourceGhostGeneration);
}

internal sealed class ItemState
{
    public long Id { get; init; }
    public double X { get; set; }
    public double Y { get; set; }
    public PowerUpDefinition Definition { get; init; } = null!;
    public double Remaining { get; set; } = 18;
    public GridPosition Cell => new((int)Math.Floor(X), (int)Math.Floor(Y));

    public ItemSnapshot ToSnapshot() =>
        new(Id, Cell, X, Y, Definition.Id, Definition.Kind, Definition.Color, Math.Max(0, Remaining));
}
