using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassSweeper.Core;
using Microsoft.UI.Dispatching;

namespace GlassSweeper_App.ViewModels;

/// <summary>
/// Presentation layer over the pure <see cref="GameViewModel"/>. Exposes
/// bindable HUD/overlay state, drives the game clock, persists settings and
/// stats, and plays win/loss sounds Ã¢â‚¬â€ mirroring SwiftSweeper's ContentView.
/// </summary>
public partial class GameBoardViewModel : ObservableObject
{
    private readonly GameViewModel _game;
    private readonly DispatcherQueueTimer _timer;
    private readonly GameSettings _settings;
    private GameState _lastState = GameState.Playing;
    private bool _isNewBest;

    public GameBoardViewModel()
    {
        _settings = SettingsService.Load();

        GameViewModel.Difficulty difficulty = ParseDifficulty(_settings.Difficulty);
        _game = new GameViewModel(difficulty);
        if (difficulty == GameViewModel.Difficulty.Custom)
        {
            _game.SetCustom(_settings.CustomRows, _settings.CustomCols, _settings.CustomMines);
        }

        _muted = _settings.Muted;
        _difficultyLabel = GameViewModel.Label(_game.CurrentDifficulty);

        _game.Changed += (_, _) => OnGameChanged();

        _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) => _game.TickSecond();
        _timer.Start();

        OnGameChanged();
    }

    /// <summary>Dimensions changed Ã¢â‚¬â€ the view should rebuild the cell grid.</summary>
    public event EventHandler? BoardReset;

    /// <summary>Cell or HUD state changed Ã¢â‚¬â€ the view should refresh visuals.</summary>
    public event EventHandler? BoardChanged;

    public GameViewModel Game => _game;

    public int Rows => _game.Rows;

    public int Cols => _game.Cols;

    // Saved custom-board dimensions, used to pre-fill the custom dialog.
    public int CustomRows => _settings.CustomRows;

    public int CustomCols => _settings.CustomCols;

    public int CustomMines => _settings.CustomMines;

    // ---- HUD ----
    [ObservableProperty]
    private string _minesRemainingText = "010";

    [ObservableProperty]
    private string _timeText = "000";

    [ObservableProperty]
    private string _faceGlyph = "\U0001F642"; // Ã°Å¸â„¢â€š

    [ObservableProperty]
    private string _difficultyLabel = "Easy";

    [ObservableProperty]
    private bool _muted;
    // Segoe Fluent Icons: Mute (E74F) / Volume (E767).
    public string MuteGlyph => Muted ? "" : "";

    // ---- Game-over overlay ----
    [ObservableProperty]
    private bool _isGameOver;

    [ObservableProperty]
    private bool _isWin;

    [ObservableProperty]
    private bool _gameOverCollapsed;

    [ObservableProperty]
    private string _overlayEmoji = string.Empty;

    [ObservableProperty]
    private string _overlayTitle = string.Empty;

    [ObservableProperty]
    private string _resultButtonText = "Play again";

    [ObservableProperty]
    private bool _showStats;

    [ObservableProperty]
    private bool _showNewBest;

    [ObservableProperty]
    private string _resultTimeText = "0s";

    [ObservableProperty]
    private string _bestTimeText = "Ã¢â‚¬â€";

    [ObservableProperty]
    private string _winsText = "0 / 0";

    [ObservableProperty]
    private string _winRateText = "Ã¢â‚¬â€";

    [ObservableProperty]
    private string _pillLabel = "Won";

    [ObservableProperty]
    private string _pillTimeText = "0s";

    // Visibility helpers (true when the overlay is shown in that mode).
    public bool ShowExpandedOverlay => IsGameOver && !GameOverCollapsed;

    public bool ShowCollapsedPill => IsGameOver && GameOverCollapsed;

    partial void OnIsGameOverChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowExpandedOverlay));
        OnPropertyChanged(nameof(ShowCollapsedPill));
    }

    partial void OnGameOverCollapsedChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowExpandedOverlay));
        OnPropertyChanged(nameof(ShowCollapsedPill));
    }

    partial void OnMutedChanged(bool value) => OnPropertyChanged(nameof(MuteGlyph));

    // ---- Commands / actions ----
    [RelayCommand]
    public void NewGame()
    {
        _game.ResetGame();
        BoardReset?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void ToggleMute()
    {
        Muted = !Muted;
        _settings.Muted = Muted;
        SettingsService.Save(_settings);
    }

    public void ToggleGameOverCollapse() => GameOverCollapsed = !GameOverCollapsed;

    public void Reveal(int row, int col) => _game.CellTapped(row, col);

    public void Flag(int row, int col) => _game.CellFlagged(row, col);

    public void Chord(int row, int col) => _game.Chord(row, col);

    public void ApplyDifficulty(GameViewModel.Difficulty difficulty)
    {
        bool willChange = difficulty != _game.CurrentDifficulty;
        _game.SetDifficulty(difficulty);
        DifficultyLabel = GameViewModel.Label(_game.CurrentDifficulty);
        _settings.Difficulty = _game.CurrentDifficulty.ToString();
        SettingsService.Save(_settings);
        if (willChange)
        {
            BoardReset?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ApplyCustom(int rows, int cols, int mines)
    {
        _game.SetCustom(rows, cols, mines);
        DifficultyLabel = GameViewModel.Label(_game.CurrentDifficulty);
        _settings.Difficulty = _game.CurrentDifficulty.ToString();
        _settings.CustomRows = _game.CustomRows;
        _settings.CustomCols = _game.CustomCols;
        _settings.CustomMines = _game.CustomMines;
        SettingsService.Save(_settings);
        BoardReset?.Invoke(this, EventArgs.Empty);
    }

    private void OnGameChanged()
    {
        GameState state = _game.GameState;

        int remaining = Math.Clamp(_game.MineCount - _game.FlagsPlaced, 0, 999);
        MinesRemainingText = remaining.ToString("D3");
        TimeText = Math.Min(_game.ElapsedTime, 999).ToString("D3");

        // Record stats once, on the transition out of Playing.
        if (_lastState == GameState.Playing && state != GameState.Playing)
        {
            _settings.TotalGames += 1;
            if (state == GameState.Won)
            {
                _settings.TotalWins += 1;
                int t = _game.ElapsedTime;
                _isNewBest = _settings.BestTime == 0 || t < _settings.BestTime;
                if (_isNewBest)
                {
                    _settings.BestTime = t;
                }

                if (!Muted)
                {
                    Sound.Win();
                }
            }
            else
            {
                _isNewBest = false;
                if (!Muted)
                {
                    Sound.Loss();
                }
            }

            SettingsService.Save(_settings);
            GameOverCollapsed = false;
        }

        _lastState = state;

        switch (state)
        {
            case GameState.Playing:
                FaceGlyph = "\U0001F642"; // Ã°Å¸â„¢â€š
                IsGameOver = false;
                break;
            case GameState.Won:
                FaceGlyph = "\U0001F60E"; // Ã°Å¸ËœÅ½
                IsWin = true;
                UpdateOverlayText(won: true);
                IsGameOver = true;
                break;
            case GameState.Lost:
                FaceGlyph = "\U0001F635"; // Ã°Å¸ËœÂµ
                IsWin = false;
                UpdateOverlayText(won: false);
                IsGameOver = true;
                break;
        }

        BoardChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateOverlayText(bool won)
    {
        OverlayEmoji = won ? "\U0001F389" : "\U0001F4A5"; // Ã°Å¸Å½â€° / Ã°Å¸â€™Â¥
        OverlayTitle = won ? "You won" : "Boom";
        ResultButtonText = won ? "Play again" : "Try again";
        PillLabel = won ? "Won" : "Lost";
        PillTimeText = FormatTime(_game.ElapsedTime);

        ShowStats = won;
        if (won)
        {
            ResultTimeText = FormatTime(_game.ElapsedTime);
            BestTimeText = _settings.BestTime > 0 ? FormatTime(_settings.BestTime) : "Ã¢â‚¬â€";
            WinsText = $"{_settings.TotalWins} / {_settings.TotalGames}";
            WinRateText = _settings.TotalGames > 0
                ? $"{(int)Math.Round(100.0 * _settings.TotalWins / _settings.TotalGames)}%"
                : "Ã¢â‚¬â€";
            ShowNewBest = _isNewBest;
        }
        else
        {
            ShowNewBest = false;
        }
    }

    private static string FormatTime(int seconds)
    {
        int m = seconds / 60;
        int s = seconds % 60;
        return m > 0 ? $"{m}:{s:D2}" : $"{s}s";
    }

    private static GameViewModel.Difficulty ParseDifficulty(string value) =>
        Enum.TryParse(value, ignoreCase: true, out GameViewModel.Difficulty d)
            ? d
            : GameViewModel.Difficulty.Easy;
}
