namespace Bomber.Core;

/// <summary>The kind of cell in the fixed 17 by 13 arena.</summary>
public enum TileType
{
    Floor,
    SolidWall,
    Crate
}

public enum PlayerKind
{
    Human,
    Computer
}

public enum AiDifficulty
{
    Novice,
    Standard,
    Expert
}

public enum GamePhase
{
    Ready,
    Playing,
    Paused,
    RoundOver,
    MatchOver
}

public enum SessionLifecycleEvent
{
    Backgrounded,
    Foregrounded
}

public enum PowerUpKind
{
    BombCapacity,
    FireRange,
    Speed,
    Kick,
    Glove,
    Remote,
    Pierce,
    BombPass,
    WallPass,
    FlamePass,
    Shield,
    Heart,
    Dash,
    Mega,
    Cluster,
    Freeze,
    Magnet,
    Mystery,
    BrickDisguise
}

/// <summary>A zero-based grid cell.</summary>
public readonly record struct GridPosition(int X, int Y)
{
    public static GridPosition operator +(GridPosition position, GridOffset offset) =>
        new(position.X + offset.X, position.Y + offset.Y);
}

public readonly record struct GridOffset(int X, int Y);

/// <summary>Held movement and edge-triggered action buttons supplied by a UI.</summary>
public readonly record struct PlayerControls(
    double Horizontal,
    double Vertical,
    bool PlaceBomb = false,
    bool UseAction = false)
{
    public static PlayerControls None => default;
}

public sealed record PlayerSlotConfiguration
{
    public string Name { get; init; } = "Player";
    public PlayerKind Kind { get; init; } = PlayerKind.Human;
    public AiDifficulty Difficulty { get; init; } = AiDifficulty.Standard;
    public string? Color { get; init; }
}

/// <summary>Settings copied by <see cref="GameSession.Configure"/> before a match starts.</summary>
public sealed record GameConfiguration
{
    public int Seed { get; init; } = 73_031;
    public int TargetCrowns { get; init; } = 3;
    public double CrateDensity { get; init; } = 0.60;
    public double ItemDropChance { get; init; } = 0.52;
    public double BombFuseSeconds { get; init; } = 1.90;
    public double FlameLifetimeSeconds { get; init; } = 0.58;
    public double SpawnProtectionSeconds { get; init; } = 1.20;
    public IReadOnlyList<PlayerSlotConfiguration> Players { get; init; } =
    [
        new() { Name = "Player 1", Kind = PlayerKind.Human },
        new() { Name = "Nova", Kind = PlayerKind.Computer, Difficulty = AiDifficulty.Standard },
        new() { Name = "Pulse", Kind = PlayerKind.Computer, Difficulty = AiDifficulty.Expert },
        new() { Name = "Byte", Kind = PlayerKind.Computer, Difficulty = AiDifficulty.Standard }
    ];

    internal GameConfiguration ValidateAndCopy()
    {
        ArgumentNullException.ThrowIfNull(Players);
        if (Players.Count is < 2 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(Players), "A match requires two to four player slots.");
        }

        if (TargetCrowns is < 1 or > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(TargetCrowns), "Target crowns must be between 1 and 9.");
        }

        if (CrateDensity is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(CrateDensity));
        }

        if (ItemDropChance is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(ItemDropChance));
        }

        if (!double.IsFinite(BombFuseSeconds) || BombFuseSeconds is < 0.10 or > 30)
        {
            throw new ArgumentOutOfRangeException(nameof(BombFuseSeconds));
        }

        if (!double.IsFinite(FlameLifetimeSeconds) || FlameLifetimeSeconds is < 0.05 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(FlameLifetimeSeconds));
        }

        if (!double.IsFinite(SpawnProtectionSeconds) || SpawnProtectionSeconds is < 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(SpawnProtectionSeconds));
        }

        var copiedPlayers = Players.Select((slot, index) =>
        {
            ArgumentNullException.ThrowIfNull(slot);
            var name = string.IsNullOrWhiteSpace(slot.Name) ? $"Player {index + 1}" : slot.Name.Trim();
            if (name.Length > 24)
            {
                name = name[..24];
            }

            return slot with { Name = name };
        }).ToArray();

        return this with { Players = copiedPlayers };
    }
}

public static class PlayerCaps
{
    public const int BombCapacity = 9;
    public const int FireRange = 10;
    public const double MoveSpeed = 5.20;
    public const int Health = 3;
    public const int Shield = 3;
    public const int DashCharges = 5;
    public const int MegaCharges = 3;
    public const int ClusterCharges = 3;
}

public sealed record PlayerStatisticsSnapshot(
    int BombsPlaced,
    int CratesDestroyed,
    int ItemsCollected,
    int Eliminations,
    int Deaths,
    int GhostBombsThrown,
    int Revivals,
    int RoundsWon);

public sealed record PlayerSnapshot(
    int Id,
    string Name,
    string Color,
    PlayerKind Kind,
    AiDifficulty Difficulty,
    double X,
    double Y,
    int FacingX,
    int FacingY,
    bool IsAlive,
    bool IsGhost,
    int Crowns,
    int Health,
    int Shield,
    double InvulnerabilitySeconds,
    double FrozenSeconds,
    int BombCapacity,
    int ActiveBombs,
    int FireRange,
    double MoveSpeed,
    bool CanKick,
    bool HasGlove,
    bool HasRemote,
    bool HasPiercingFlames,
    bool CanPassBombs,
    bool CanPassCrates,
    bool IsFlameproof,
    bool HasMagnet,
    bool HasBrickDisguise,
    int DashCharges,
    int MegaCharges,
    int ClusterCharges,
    bool IsGhostBombReady,
    string AiIntent,
    PlayerStatisticsSnapshot Statistics)
{
    public GridPosition Cell => new((int)Math.Floor(X), (int)Math.Floor(Y));
    public int BombsAvailable => Math.Max(0, BombCapacity - ActiveBombs);
}

public sealed record BombSnapshot(
    long Id,
    int OwnerPlayerId,
    GridPosition Cell,
    double X,
    double Y,
    double FuseSeconds,
    double InitialFuseSeconds,
    int FireRange,
    bool IsMega,
    bool IsCluster,
    bool IsPiercing,
    bool IsBrickDisguised,
    bool IsMoving,
    bool IsGhost,
    bool IsAirborne,
    double AirborneProgress,
    IReadOnlyList<GridPosition> FlamePreviewCells);

public sealed record FlameSnapshot(
    GridPosition Cell,
    double RemainingSeconds,
    int SourcePlayerId,
    long SourceBombId,
    bool IsMega,
    bool IsGhostSource,
    int SourceGhostGeneration);

public sealed record ItemSnapshot(
    long Id,
    GridPosition Cell,
    double X,
    double Y,
    string PowerUpId,
    PowerUpKind Kind,
    string Color,
    double RemainingSeconds);

public sealed record RoundResultSnapshot(
    int RoundNumber,
    int? WinnerPlayerId,
    bool IsDraw,
    int? MatchWinnerPlayerId);

public sealed class BoardSnapshot
{
    private readonly TileType[] _tiles;

    internal BoardSnapshot(int width, int height, TileType[] tiles)
    {
        Width = width;
        Height = height;
        _tiles = tiles;
    }

    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<TileType> Tiles => _tiles;

    public TileType this[int x, int y] =>
        x >= 0 && x < Width && y >= 0 && y < Height
            ? _tiles[(y * Width) + x]
            : throw new ArgumentOutOfRangeException($"Cell ({x}, {y}) is outside the board.");

    public TileType this[GridPosition position] => this[position.X, position.Y];
}

public sealed record GameSnapshot(
    GamePhase Phase,
    bool IsPaused,
    int RoundNumber,
    int TargetCrowns,
    double ElapsedSeconds,
    int? MatchWinnerPlayerId,
    RoundResultSnapshot? LastRound,
    BoardSnapshot Board,
    IReadOnlyList<PlayerSnapshot> Players,
    IReadOnlyList<BombSnapshot> Bombs,
    IReadOnlyList<FlameSnapshot> Flames,
    IReadOnlyList<ItemSnapshot> Items);
