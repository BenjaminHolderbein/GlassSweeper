namespace GlassSweeper.Core;

/// <summary>
/// Pure, UI-independent Minesweeper game logic, ported from SwiftSweeperKit's
/// <c>GameViewModel</c>. Exposes plain state plus a single <see cref="Changed"/>
/// event; an MVVM presentation layer wraps this for data binding. The timer is
/// modeled as an <see cref="IsTimerRunning"/> flag driven externally via
/// <see cref="TickSecond"/>, keeping the type free of threading and fully testable.
/// </summary>
public sealed class GameViewModel
{
    public enum Difficulty
    {
        Easy,
        Medium,
        Hard,
        Custom,
    }

    public const int MinBoardSide = 5;
    public const int MaxBoardSide = 30;

    /// <summary>Built-in preset dimensions. <c>Custom</c> returns null.</summary>
    private static (int Rows, int Cols, int Mines)? Preset(Difficulty d) =>
        d switch
        {
            Difficulty.Easy => (9, 9, 10),
            Difficulty.Medium => (13, 13, 25),
            Difficulty.Hard => (16, 16, 45),
            _ => null,
        };

    public static string Label(Difficulty d) =>
        d switch
        {
            Difficulty.Easy => "Easy",
            Difficulty.Medium => "Medium",
            Difficulty.Hard => "Hard",
            Difficulty.Custom => "Custom",
            _ => d.ToString(),
        };

    /// <summary>
    /// Largest mine count that still leaves room for the first-tap safe zone
    /// (the clicked cell + its 8 neighbors).
    /// </summary>
    public static int MaxMines(int rows, int cols) => Math.Max(1, (rows * cols) - 9);

    private readonly Random _random;
    private bool _isFirstTap = true;
    private bool _timerRunning;

    public GameViewModel(Difficulty difficulty = Difficulty.Easy, Random? random = null)
    {
        _random = random ?? new Random();
        CurrentDifficulty = difficulty;
        ResetGame();
    }

    /// <summary>Raised after any public mutation so a UI layer can refresh.</summary>
    public event EventHandler? Changed;

    public Difficulty CurrentDifficulty { get; private set; } = Difficulty.Easy;
    public int CustomRows { get; private set; } = 12;
    public int CustomCols { get; private set; } = 12;
    public int CustomMines { get; private set; } = 20;

    public int Rows => Preset(CurrentDifficulty)?.Rows ?? CustomRows;
    public int Cols => Preset(CurrentDifficulty)?.Cols ?? CustomCols;
    public int MineCount => Preset(CurrentDifficulty)?.Mines ?? CustomMines;

    public Cell[][] Grid { get; private set; } = [];
    public GameState GameState { get; private set; } = GameState.Playing;
    public int FlagsPlaced { get; private set; }
    public int ElapsedTime { get; private set; }

    public bool IsTimerRunning => _timerRunning;

    public void SetDifficulty(Difficulty d)
    {
        if (d == CurrentDifficulty)
        {
            return;
        }

        CurrentDifficulty = d;
        ResetGame();
    }

    public void SetCustom(int rows, int cols, int mines)
    {
        int r = Math.Clamp(rows, MinBoardSide, MaxBoardSide);
        int c = Math.Clamp(cols, MinBoardSide, MaxBoardSide);
        int m = Math.Clamp(mines, 1, MaxMines(r, c));
        CustomRows = r;
        CustomCols = c;
        CustomMines = m;
        if (CurrentDifficulty != Difficulty.Custom)
        {
            CurrentDifficulty = Difficulty.Custom;
        }

        ResetGame();
    }

    public void ResetGame()
    {
        StopGameTimer();
        GameState = GameState.Playing;
        FlagsPlaced = 0;
        ElapsedTime = 0;
        _isFirstTap = true;
        Grid = new Cell[Rows][];
        for (int r = 0; r < Rows; r++)
        {
            Grid[r] = new Cell[Cols];
        }

        RaiseChanged();
    }

    public void CellTapped(int row, int col)
    {
        if (GameState != GameState.Playing || !IsValid(row, col))
        {
            return;
        }

        TapInternal(row, col);
        RaiseChanged();
    }

    public void CellFlagged(int row, int col)
    {
        if (GameState != GameState.Playing || !IsValid(row, col))
        {
            return;
        }

        if (Grid[row][col].IsRevealed)
        {
            return;
        }

        CellMark prev = Grid[row][col].Mark;
        Grid[row][col].Mark = prev switch
        {
            CellMark.None => CellMark.Flag,
            CellMark.Flag => CellMark.Question,
            _ => CellMark.None,
        };

        if (prev == CellMark.Flag)
        {
            FlagsPlaced -= 1;
        }

        if (Grid[row][col].Mark == CellMark.Flag)
        {
            FlagsPlaced += 1;
        }

        RaiseChanged();
    }

    /// <summary>
    /// Classic chord: when the user activates a revealed numbered cell whose
    /// adjacent flag count matches its mine count, reveal every adjacent
    /// un-flagged, un-revealed cell. Mis-flagging steps on a real mine — game over.
    /// </summary>
    public void Chord(int row, int col)
    {
        if (GameState != GameState.Playing || !IsValid(row, col))
        {
            return;
        }

        Cell cell = Grid[row][col];
        if (!cell.IsRevealed || cell.NeighboringMines <= 0)
        {
            return;
        }

        int adjacentFlags = 0;
        for (int dr = -1; dr <= 1; dr++)
        {
            for (int dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0)
                {
                    continue;
                }

                int nr = row + dr;
                int nc = col + dc;
                if (IsValid(nr, nc) && Grid[nr][nc].IsFlagged)
                {
                    adjacentFlags += 1;
                }
            }
        }

        if (adjacentFlags != cell.NeighboringMines)
        {
            return;
        }

        for (int dr = -1; dr <= 1; dr++)
        {
            for (int dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0)
                {
                    continue;
                }

                int nr = row + dr;
                int nc = col + dc;
                if (!IsValid(nr, nc))
                {
                    continue;
                }

                Cell n = Grid[nr][nc];
                if (!n.IsFlagged && !n.IsRevealed)
                {
                    TapInternal(nr, nc);
                    if (GameState != GameState.Playing)
                    {
                        RaiseChanged();
                        return;
                    }
                }
            }
        }

        RaiseChanged();
    }

    /// <summary>Advances the game clock by one second when the timer is running.</summary>
    public void TickSecond()
    {
        if (GameState == GameState.Playing && _timerRunning)
        {
            ElapsedTime += 1;
            RaiseChanged();
        }
    }

    private void TapInternal(int row, int col)
    {
        if (_isFirstTap)
        {
            _isFirstTap = false;
            PlaceMines(row, col);
            CalculateNeighborCounts();
            StartGameTimer();
        }

        if (Grid[row][col].IsRevealed || Grid[row][col].IsFlagged)
        {
            return;
        }

        Grid[row][col].IsRevealed = true;
        if (Grid[row][col].IsMine)
        {
            Grid[row][col].IsExploded = true;
            TriggerGameOver(won: false);
            return;
        }

        if (Grid[row][col].NeighboringMines == 0)
        {
            RevealAdjacentCells(row, col);
        }

        CheckWinCondition();
    }

    private void PlaceMines(int avoidRow, int avoidCol)
    {
        var safe = new HashSet<int>();
        for (int dr = -1; dr <= 1; dr++)
        {
            for (int dc = -1; dc <= 1; dc++)
            {
                int nr = avoidRow + dr;
                int nc = avoidCol + dc;
                if (IsValid(nr, nc))
                {
                    safe.Add((nr * Cols) + nc);
                }
            }
        }

        var locations = new List<(int R, int C)>();
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Cols; c++)
            {
                if (!safe.Contains((r * Cols) + c))
                {
                    locations.Add((r, c));
                }
            }
        }

        // Fisher-Yates shuffle (mirrors Swift's Array.shuffle()).
        for (int i = locations.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (locations[i], locations[j]) = (locations[j], locations[i]);
        }

        int count = Math.Min(MineCount, locations.Count);
        for (int i = 0; i < count; i++)
        {
            (int r, int c) = locations[i];
            Grid[r][c].IsMine = true;
        }
    }

    private void CalculateNeighborCounts()
    {
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Cols; c++)
            {
                if (Grid[r][c].IsMine)
                {
                    continue;
                }

                int count = 0;
                for (int dr = -1; dr <= 1; dr++)
                {
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        if (dr == 0 && dc == 0)
                        {
                            continue;
                        }

                        int nr = r + dr;
                        int nc = c + dc;
                        if (IsValid(nr, nc) && Grid[nr][nc].IsMine)
                        {
                            count += 1;
                        }
                    }
                }

                Grid[r][c].NeighboringMines = count;
            }
        }
    }

    private void RevealAdjacentCells(int row, int col)
    {
        for (int dr = -1; dr <= 1; dr++)
        {
            for (int dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0)
                {
                    continue;
                }

                int nr = row + dr;
                int nc = col + dc;
                if (IsValid(nr, nc) && !Grid[nr][nc].IsRevealed && !Grid[nr][nc].IsFlagged)
                {
                    Grid[nr][nc].IsRevealed = true;
                    if (Grid[nr][nc].NeighboringMines == 0)
                    {
                        RevealAdjacentCells(nr, nc);
                    }
                }
            }
        }
    }

    private void CheckWinCondition()
    {
        if (GameState != GameState.Playing)
        {
            return;
        }

        int revealedCount = 0;
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Cols; c++)
            {
                if (Grid[r][c].IsRevealed && !Grid[r][c].IsMine)
                {
                    revealedCount += 1;
                }
            }
        }

        if (revealedCount == (Rows * Cols) - MineCount)
        {
            TriggerGameOver(won: true);
        }
    }

    private void TriggerGameOver(bool won)
    {
        if (GameState != GameState.Playing)
        {
            return;
        }

        GameState = won ? GameState.Won : GameState.Lost;
        StopGameTimer();
        if (won)
        {
            AutoFlagRemainingMines();
        }
        else
        {
            RevealAllMines();
        }
    }

    private void RevealAllMines()
    {
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Cols; c++)
            {
                if (Grid[r][c].IsMine)
                {
                    Grid[r][c].IsRevealed = true;
                }
            }
        }
    }

    private void AutoFlagRemainingMines()
    {
        int newFlags = 0;
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Cols; c++)
            {
                if (Grid[r][c].IsMine && !Grid[r][c].IsFlagged)
                {
                    Grid[r][c].Mark = CellMark.Flag;
                    newFlags += 1;
                }
            }
        }

        FlagsPlaced += newFlags;
    }

    private void StartGameTimer()
    {
        ElapsedTime = 0;
        _timerRunning = true;
    }

    private void StopGameTimer() => _timerRunning = false;

    private bool IsValid(int row, int col) => row >= 0 && row < Rows && col >= 0 && col < Cols;

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
