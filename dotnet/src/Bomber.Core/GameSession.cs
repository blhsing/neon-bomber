namespace Bomber.Core;

/// <summary>
/// Owns all arena state, deterministic random choices, input, AI, and fixed-step simulation for a match.
/// A Blazor component can bind directly to <see cref="Snapshot"/> and call <see cref="Tick"/> from its render loop.
/// </summary>
public sealed partial class GameSession
{
    private const double FixedStepSeconds = 1.0 / 60.0;
    public const double MaximumInteractiveFrameSeconds = 1.0 / 30.0;
    private static readonly string[] DefaultColors = ["#35f6ff", "#ff4fa3", "#9dff55", "#ffd34f"];

    private GameConfiguration _configuration;
    private DeterministicRandom _random;
    private readonly ArenaBoard _board = new();
    private readonly List<PlayerState> _players = [];
    private readonly List<BombState> _bombs = [];
    private readonly List<FlameState> _flames = [];
    private readonly List<ItemState> _items = [];
    private readonly HashSet<long> _usedGhostRevivalSources = [];
    private BoardSnapshot? _boardSnapshot;
    private double _accumulator;
    private double _elapsedSeconds;
    private long _nextBombId;
    private long _nextItemId;
    private bool _pausedByLifecycle;

    public GameSession(GameConfiguration? configuration = null)
    {
        _configuration = (configuration ?? new GameConfiguration()).ValidateAndCopy();
        _random = new DeterministicRandom(_configuration.Seed);
    }

    public GameConfiguration Configuration => _configuration;
    public GamePhase Phase { get; private set; } = GamePhase.Ready;
    public bool IsPaused => Phase == GamePhase.Paused;
    public int RoundNumber { get; private set; }
    public int? MatchWinnerPlayerId { get; private set; }
    public RoundResultSnapshot? LastRound { get; private set; }
    public long Version { get; private set; }
    public BoardSnapshot Board => _boardSnapshot ??= _board.ToSnapshot();
    public IReadOnlyList<PlayerSnapshot> Players => _players.Select(player => player.ToSnapshot()).ToArray();
    public IReadOnlyList<BombSnapshot> Bombs => _bombs
        .Where(bomb => !bomb.IsExploded)
        .Select(CreateBombSnapshot)
        .ToArray();
    public IReadOnlyList<FlameSnapshot> Flames => _flames.Select(flame => flame.ToSnapshot()).ToArray();
    public IReadOnlyList<ItemSnapshot> Items => _items.Select(item => item.ToSnapshot()).ToArray();

    public GameSnapshot Snapshot =>
        new(
            Phase,
            IsPaused,
            RoundNumber,
            _configuration.TargetCrowns,
            _elapsedSeconds,
            MatchWinnerPlayerId,
            LastRound,
            Board,
            Players,
            Bombs,
            Flames,
            Items);

    public void Configure(GameConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (Phase is GamePhase.Playing or GamePhase.Paused)
        {
            throw new InvalidOperationException("An active match must finish before it can be reconfigured.");
        }

        _configuration = configuration.ValidateAndCopy();
        _random = new DeterministicRandom(_configuration.Seed);
        ResetToReady();
    }

    public void StartMatch()
    {
        if (Phase is GamePhase.Playing or GamePhase.Paused)
        {
            throw new InvalidOperationException("A match is already active.");
        }

        _random = new DeterministicRandom(_configuration.Seed);
        _players.Clear();
        for (var id = 0; id < _configuration.Players.Count; id++)
        {
            var slot = _configuration.Players[id];
            _players.Add(new PlayerState
            {
                Id = id,
                Name = slot.Name,
                Kind = slot.Kind,
                Difficulty = slot.Difficulty,
                Color = string.IsNullOrWhiteSpace(slot.Color) ? DefaultColors[id] : slot.Color
            });
        }

        RoundNumber = 1;
        MatchWinnerPlayerId = null;
        LastRound = null;
        _elapsedSeconds = 0;
        _nextBombId = 0;
        _nextItemId = 0;
        BeginRound();
    }

    public void StartNextRound()
    {
        if (Phase != GamePhase.RoundOver)
        {
            throw new InvalidOperationException("The next round can only start after a round that did not end the match.");
        }

        RoundNumber++;
        BeginRound();
    }

    /// <summary>Updates held movement and latches rising edges for the two action buttons.</summary>
    public bool SetControls(int playerId, PlayerControls controls)
    {
        if (!double.IsFinite(controls.Horizontal) || !double.IsFinite(controls.Vertical))
        {
            throw new ArgumentOutOfRangeException(nameof(controls), "Control axes must be finite numbers.");
        }

        var player = _players.FirstOrDefault(candidate => candidate.Id == playerId);
        if (player is null || player.Kind != PlayerKind.Human)
        {
            return false;
        }

        var normalized = controls with
        {
            Horizontal = Math.Clamp(controls.Horizontal, -1, 1),
            Vertical = Math.Clamp(controls.Vertical, -1, 1)
        };
        if (normalized.PlaceBomb && !player.BombButtonHeld)
        {
            player.BombRequested = true;
        }

        if (normalized.UseAction && !player.ActionButtonHeld)
        {
            player.ActionRequested = true;
        }

        player.BombButtonHeld = normalized.PlaceBomb;
        player.ActionButtonHeld = normalized.UseAction;
        player.Controls = normalized;
        return true;
    }

    /// <summary>Advances a deterministic 60 Hz simulation. Long browser gaps should be paired with lifecycle pause.</summary>
    public void Tick(double elapsedSeconds)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0 || elapsedSeconds > 30)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), "Tick duration must be between zero and 30 seconds.");
        }

        if (Phase != GamePhase.Playing || elapsedSeconds == 0)
        {
            return;
        }

        _accumulator += elapsedSeconds;
        while (_accumulator + 1e-12 >= FixedStepSeconds && Phase == GamePhase.Playing)
        {
            Step(FixedStepSeconds);
            _accumulator -= FixedStepSeconds;
        }

        if (_accumulator < 0)
        {
            _accumulator = 0;
        }
    }

    /// <summary>
    /// Advances an interactive client without replaying time that elapsed while its UI thread
    /// could not accept input. Deterministic callers that intentionally need catch-up can still
    /// use <see cref="Tick(double)"/> directly.
    /// </summary>
    public void TickInteractive(double elapsedSeconds)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), "Interactive tick duration must be finite and non-negative.");
        }

        Tick(Math.Min(elapsedSeconds, MaximumInteractiveFrameSeconds));
    }

    public bool Pause()
    {
        if (Phase != GamePhase.Playing)
        {
            return false;
        }

        Phase = GamePhase.Paused;
        Version++;
        return true;
    }

    public bool Resume()
    {
        if (Phase != GamePhase.Paused)
        {
            return false;
        }

        Phase = GamePhase.Playing;
        Version++;
        return true;
    }

    public bool SetPaused(bool paused) => paused ? Pause() : Resume();

    public void HandleLifecycle(SessionLifecycleEvent lifecycleEvent)
    {
        switch (lifecycleEvent)
        {
            case SessionLifecycleEvent.Backgrounded:
                _pausedByLifecycle = Phase == GamePhase.Playing;
                if (_pausedByLifecycle)
                {
                    Pause();
                }

                break;
            case SessionLifecycleEvent.Foregrounded:
                if (_pausedByLifecycle)
                {
                    _pausedByLifecycle = false;
                    Resume();
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(lifecycleEvent));
        }
    }

    private void BeginRound()
    {
        _board.Generate(_random, _configuration.CrateDensity);
        InvalidateBoardSnapshot();
        _bombs.Clear();
        _flames.Clear();
        _items.Clear();
        _usedGhostRevivalSources.Clear();
        _accumulator = 0;
        _pausedByLifecycle = false;
        LastRound = null;
        for (var index = 0; index < _players.Count; index++)
        {
            _players[index].ResetForRound(ArenaRules.SpawnCells[index], _configuration.SpawnProtectionSeconds);
            if (_players[index].Kind == PlayerKind.Computer)
            {
                // Keep multiple bots from doing their most expensive route planning on the
                // same browser frame. The offset remains deterministic and is imperceptible.
                _players[index].AiThinkRemaining = index * FixedStepSeconds;
            }
        }

        Phase = GamePhase.Playing;
        Version++;
    }

    private void InvalidateBoardSnapshot() => _boardSnapshot = null;

    private void ResetToReady()
    {
        _players.Clear();
        _bombs.Clear();
        _flames.Clear();
        _items.Clear();
        _usedGhostRevivalSources.Clear();
        _accumulator = 0;
        _elapsedSeconds = 0;
        _nextBombId = 0;
        _nextItemId = 0;
        _pausedByLifecycle = false;
        RoundNumber = 0;
        MatchWinnerPlayerId = null;
        LastRound = null;
        Phase = GamePhase.Ready;
        Version++;
    }
}
