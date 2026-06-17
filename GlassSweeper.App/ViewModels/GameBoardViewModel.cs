using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassSweeper.Core;
using Microsoft.UI.Dispatching;

namespace GlassSweeper_App.ViewModels;

/// <summary>
/// Presentation layer over the pure <see cref="GameViewModel"/>. Exposes
/// bindable HUD state, drives the game clock with a UI-thread timer, and
/// surfaces two events the view uses to keep the board in sync:
/// <see cref="BoardReset"/> (dimensions changed — rebuild the grid) and
/// <see cref="BoardChanged"/> (cell/HUD state changed — refresh visuals).
/// </summary>
public partial class GameBoardViewModel : ObservableObject
{
    private readonly GameViewModel _game = new(GameViewModel.Difficulty.Easy);
    private readonly DispatcherQueueTimer _timer;

    public GameBoardViewModel()
    {
        _game.Changed += (_, _) => OnGameChanged();

        _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) => _game.TickSecond();
        _timer.Start();

        OnGameChanged();
    }

    /// <summary>Dimensions changed — the view should rebuild the cell grid.</summary>
    public event EventHandler? BoardReset;

    /// <summary>Cell or HUD state changed — the view should refresh visuals.</summary>
    public event EventHandler? BoardChanged;

    /// <summary>The underlying game model, for the view to read cell state.</summary>
    public GameViewModel Game => _game;

    public int Rows => _game.Rows;

    public int Cols => _game.Cols;

    [ObservableProperty]
    private string _minesRemainingText = "010";

    [ObservableProperty]
    private string _timeText = "000";

    [ObservableProperty]
    private string _faceGlyph = "\U0001F642"; // 🙂

    [ObservableProperty]
    private bool _isGameOver;

    [ObservableProperty]
    private bool _isWin;

    [ObservableProperty]
    private string _overlayTitle = string.Empty;

    [ObservableProperty]
    private string _overlaySubtitle = string.Empty;

    [ObservableProperty]
    private string _difficultyLabel = "Easy";

    public void Reveal(int row, int col) => _game.CellTapped(row, col);

    public void Flag(int row, int col) => _game.CellFlagged(row, col);

    public void Chord(int row, int col) => _game.Chord(row, col);

    [RelayCommand]
    public void NewGame()
    {
        _game.ResetGame();
        BoardReset?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyDifficulty(GameViewModel.Difficulty difficulty)
    {
        bool dimsWillChange = difficulty != _game.CurrentDifficulty;
        _game.SetDifficulty(difficulty);
        DifficultyLabel = GameViewModel.Label(_game.CurrentDifficulty);

        // SetDifficulty no-ops (no reset) when the difficulty is unchanged;
        // only rebuild the grid when it actually changed.
        if (dimsWillChange)
        {
            BoardReset?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ApplyCustom(int rows, int cols, int mines)
    {
        _game.SetCustom(rows, cols, mines);
        DifficultyLabel = GameViewModel.Label(_game.CurrentDifficulty);
        BoardReset?.Invoke(this, EventArgs.Empty);
    }

    private void OnGameChanged()
    {
        int remaining = _game.MineCount - _game.FlagsPlaced;
        MinesRemainingText = Format3(remaining);
        TimeText = Format3(Math.Min(_game.ElapsedTime, 999));

        switch (_game.GameState)
        {
            case GameState.Playing:
                FaceGlyph = "\U0001F642"; // 🙂
                IsGameOver = false;
                break;
            case GameState.Won:
                FaceGlyph = "\U0001F60E"; // 😎
                IsGameOver = true;
                IsWin = true;
                OverlayTitle = "You Win!";
                OverlaySubtitle = $"Cleared in {_game.ElapsedTime}s";
                break;
            case GameState.Lost:
                FaceGlyph = "\U0001F635"; // 😵
                IsGameOver = true;
                IsWin = false;
                OverlayTitle = "Boom!";
                OverlaySubtitle = "You hit a mine.";
                break;
        }

        BoardChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string Format3(int value)
    {
        if (value < 0)
        {
            // Classic counters show a leading minus, e.g. "-05".
            int magnitude = Math.Min(-value, 99);
            return "-" + magnitude.ToString("D2");
        }

        return value.ToString("D3");
    }
}
