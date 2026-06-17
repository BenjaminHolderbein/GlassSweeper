using GlassSweeper.Core;
using Xunit;

namespace GlassSweeper.Tests;

public class GameViewModelTests
{
    [Fact]
    public void TestInitialState()
    {
        var vm = new GameViewModel(GameViewModel.Difficulty.Easy);
        Assert.Equal(GameState.Playing, vm.GameState);
        Assert.Equal(9, vm.Grid.Length);
        Assert.Equal(9, vm.Grid[0].Length);
        Assert.Equal(0, vm.FlagsPlaced);
        Assert.Equal(0, vm.ElapsedTime);
        // Mines aren't placed until the first tap.
        Assert.Equal(0, TotalMines(vm));
    }

    [Fact]
    public void TestFirstTapPlacesMinesAndSpareNeighbors()
    {
        var vm = new GameViewModel(GameViewModel.Difficulty.Easy);
        vm.CellTapped(4, 4);
        Assert.Equal(10, TotalMines(vm));
        for (int dr = -1; dr <= 1; dr++)
        {
            for (int dc = -1; dc <= 1; dc++)
            {
                int r = 4 + dr, c = 4 + dc;
                Assert.False(vm.Grid[r][c].IsMine, $"({r},{c}) should be safe on first tap");
            }
        }

        Assert.Equal(GameState.Playing, vm.GameState);
        Assert.True(vm.Grid[4][4].IsRevealed);
    }

    [Fact]
    public void TestFlagCycle()
    {
        var vm = new GameViewModel(GameViewModel.Difficulty.Easy);
        vm.CellTapped(0, 0);
        if (FirstUnrevealed(vm) is not (int r, int c))
        {
            Assert.Fail("no unrevealed cell");
            return;
        }

        Assert.Equal(CellMark.None, vm.Grid[r][c].Mark);
        vm.CellFlagged(r, c);
        Assert.Equal(CellMark.Flag, vm.Grid[r][c].Mark);
        Assert.Equal(1, vm.FlagsPlaced);

        vm.CellFlagged(r, c);
        Assert.Equal(CellMark.Question, vm.Grid[r][c].Mark);
        Assert.Equal(0, vm.FlagsPlaced);

        vm.CellFlagged(r, c);
        Assert.Equal(CellMark.None, vm.Grid[r][c].Mark);
        Assert.Equal(0, vm.FlagsPlaced);
    }

    [Fact]
    public void TestFlaggedCellIsProtectedFromReveal()
    {
        var vm = new GameViewModel(GameViewModel.Difficulty.Easy);
        vm.CellTapped(0, 0);
        if (FirstUnrevealed(vm) is not (int r, int c))
        {
            Assert.Fail();
            return;
        }

        vm.CellFlagged(r, c);
        vm.CellTapped(r, c);
        Assert.False(vm.Grid[r][c].IsRevealed);
    }

    [Fact]
    public void TestHittingMineLosesAndMarksOnlyOneExploded()
    {
        var vm = new GameViewModel(GameViewModel.Difficulty.Easy);
        vm.CellTapped(0, 0);
        if (FirstMine(vm) is not (int r, int c))
        {
            Assert.Fail("no mines placed");
            return;
        }

        vm.CellTapped(r, c);
        Assert.Equal(GameState.Lost, vm.GameState);
        Assert.True(vm.Grid[r][c].IsExploded);

        int exploded = 0;
        int unrevealedMines = 0;
        for (int rr = 0; rr < vm.Rows; rr++)
        {
            for (int cc = 0; cc < vm.Cols; cc++)
            {
                if (vm.Grid[rr][cc].IsExploded)
                {
                    exploded++;
                }

                if (vm.Grid[rr][cc].IsMine && !vm.Grid[rr][cc].IsRevealed)
                {
                    unrevealedMines++;
                }
            }
        }

        Assert.Equal(1, exploded);
        Assert.Equal(0, unrevealedMines);
    }

    [Fact]
    public void TestRevealingAllNonMinesWins()
    {
        var vm = new GameViewModel(GameViewModel.Difficulty.Easy);
        vm.CellTapped(4, 4);
        for (int r = 0; r < 9; r++)
        {
            for (int c = 0; c < 9; c++)
            {
                if (!vm.Grid[r][c].IsMine && !vm.Grid[r][c].IsRevealed)
                {
                    vm.CellTapped(r, c);
                }
            }
        }

        Assert.Equal(GameState.Won, vm.GameState);
        int unflaggedMines = 0;
        for (int r = 0; r < vm.Rows; r++)
        {
            for (int c = 0; c < vm.Cols; c++)
            {
                if (vm.Grid[r][c].IsMine && !vm.Grid[r][c].IsFlagged)
                {
                    unflaggedMines++;
                }
            }
        }

        Assert.Equal(0, unflaggedMines);
        Assert.Equal(10, vm.FlagsPlaced);
    }

    [Fact]
    public void TestSetDifficultyResetsBoard()
    {
        var vm = new GameViewModel(GameViewModel.Difficulty.Easy);
        vm.CellTapped(0, 0);
        vm.SetDifficulty(GameViewModel.Difficulty.Medium);
        Assert.Equal(GameState.Playing, vm.GameState);
        Assert.Equal(13, vm.Grid.Length);
        Assert.Equal(13, vm.Grid[0].Length);
        Assert.Equal(0, vm.FlagsPlaced);
        Assert.Equal(0, RevealedCount(vm));
    }

    [Fact]
    public void TestMineCountMatchesDifficulty()
    {
        var cases = new (GameViewModel.Difficulty Diff, int Expected)[]
        {
            (GameViewModel.Difficulty.Easy, 10),
            (GameViewModel.Difficulty.Medium, 25),
            (GameViewModel.Difficulty.Hard, 45),
        };

        foreach ((GameViewModel.Difficulty diff, int expected) in cases)
        {
            var vm = new GameViewModel(diff);
            vm.CellTapped(1, 1);
            Assert.Equal(expected, TotalMines(vm));
        }
    }

    [Fact]
    public void TestNeighborCountsMatchPlacedMines()
    {
        var vm = new GameViewModel(GameViewModel.Difficulty.Easy);
        vm.CellTapped(4, 4);
        for (int r = 0; r < vm.Rows; r++)
        {
            for (int c = 0; c < vm.Cols; c++)
            {
                if (vm.Grid[r][c].IsMine)
                {
                    continue;
                }

                int expected = 0;
                for (int dr = -1; dr <= 1; dr++)
                {
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        if (dr == 0 && dc == 0)
                        {
                            continue;
                        }

                        int nr = r + dr, nc = c + dc;
                        if (nr >= 0 && nr < vm.Rows && nc >= 0 && nc < vm.Cols && vm.Grid[nr][nc].IsMine)
                        {
                            expected++;
                        }
                    }
                }

                Assert.Equal(expected, vm.Grid[r][c].NeighboringMines);
            }
        }
    }

    [Fact]
    public void TestTimerStartsOnFirstTap()
    {
        var vm = new GameViewModel(GameViewModel.Difficulty.Easy);
        Assert.False(vm.IsTimerRunning);
        vm.CellTapped(4, 4);
        Assert.True(vm.IsTimerRunning);
    }

    [Fact]
    public void TestTimerStopsOnWin()
    {
        var vm = new GameViewModel(GameViewModel.Difficulty.Easy);
        vm.CellTapped(4, 4);
        for (int r = 0; r < 9; r++)
        {
            for (int c = 0; c < 9; c++)
            {
                if (!vm.Grid[r][c].IsMine && !vm.Grid[r][c].IsRevealed)
                {
                    vm.CellTapped(r, c);
                }
            }
        }

        Assert.Equal(GameState.Won, vm.GameState);
        Assert.False(vm.IsTimerRunning);
    }

    [Fact]
    public void TestTimerStopsOnLoss()
    {
        var vm = new GameViewModel(GameViewModel.Difficulty.Easy);
        vm.CellTapped(0, 0);
        if (FirstMine(vm) is not (int r, int c))
        {
            Assert.Fail();
            return;
        }

        vm.CellTapped(r, c);
        Assert.Equal(GameState.Lost, vm.GameState);
        Assert.False(vm.IsTimerRunning);
    }

    [Fact]
    public void TestTimerStopsOnResetAndSetDifficulty()
    {
        var vm = new GameViewModel(GameViewModel.Difficulty.Easy);
        vm.CellTapped(0, 0);
        Assert.True(vm.IsTimerRunning);
        vm.ResetGame();
        Assert.False(vm.IsTimerRunning);

        vm.CellTapped(0, 0);
        Assert.True(vm.IsTimerRunning);
        vm.SetDifficulty(GameViewModel.Difficulty.Medium);
        Assert.False(vm.IsTimerRunning);
    }

    [Fact]
    public void TestChordRevealsAdjacentWhenFlagsMatch()
    {
        var vm = new GameViewModel(GameViewModel.Difficulty.Easy);
        vm.CellTapped(4, 4);
        if (NumberedRevealed(vm) is not (int r, int c))
        {
            Assert.Fail("no numbered revealed cell");
            return;
        }

        int n = vm.Grid[r][c].NeighboringMines;
        // Flag exactly the adjacent mines.
        for (int dr = -1; dr <= 1; dr++)
        {
            for (int dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0)
                {
                    continue;
                }

                int nr = r + dr, nc = c + dc;
                if (nr >= 0 && nr < vm.Rows && nc >= 0 && nc < vm.Cols && vm.Grid[nr][nc].IsMine)
                {
                    vm.CellFlagged(nr, nc);
                }
            }
        }

        int flags = 0;
        for (int dr = -1; dr <= 1; dr++)
        {
            for (int dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0)
                {
                    continue;
                }

                int nr = r + dr, nc = c + dc;
                if (nr >= 0 && nr < vm.Rows && nc >= 0 && nc < vm.Cols && vm.Grid[nr][nc].IsFlagged)
                {
                    flags++;
                }
            }
        }

        Assert.Equal(n, flags);
        vm.Chord(r, c);
        // After chord, every non-flagged adjacent cell should be revealed.
        for (int dr = -1; dr <= 1; dr++)
        {
            for (int dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0)
                {
                    continue;
                }

                int nr = r + dr, nc = c + dc;
                if (nr < 0 || nr >= vm.Rows || nc < 0 || nc >= vm.Cols)
                {
                    continue;
                }

                if (!vm.Grid[nr][nc].IsFlagged)
                {
                    Assert.True(vm.Grid[nr][nc].IsRevealed, $"({nr},{nc}) should be revealed after chord");
                }
            }
        }
    }

    [Fact]
    public void TestChordCanCauseLoss()
    {
        for (int attempt = 0; attempt < 200; attempt++)
        {
            var vm = new GameViewModel(GameViewModel.Difficulty.Easy);
            vm.CellTapped(4, 4);
            if (NumberedRevealed(vm) is not (int r, int c))
            {
                continue;
            }

            int n = vm.Grid[r][c].NeighboringMines;
            var nonMineAdjacent = new List<(int R, int C)>();
            for (int dr = -1; dr <= 1; dr++)
            {
                for (int dc = -1; dc <= 1; dc++)
                {
                    if (dr == 0 && dc == 0)
                    {
                        continue;
                    }

                    int nr = r + dr, nc = c + dc;
                    if (nr >= 0 && nr < vm.Rows && nc >= 0 && nc < vm.Cols
                        && !vm.Grid[nr][nc].IsMine && !vm.Grid[nr][nc].IsRevealed)
                    {
                        nonMineAdjacent.Add((nr, nc));
                    }
                }
            }

            if (nonMineAdjacent.Count < n)
            {
                continue;
            }

            for (int i = 0; i < n; i++)
            {
                vm.CellFlagged(nonMineAdjacent[i].R, nonMineAdjacent[i].C);
            }

            vm.Chord(r, c);
            Assert.NotEqual(GameState.Playing, vm.GameState);
            return;
        }

        Assert.Fail("could not construct a chord-loss scenario in 200 tries");
    }

    [Fact]
    public void TestChordNoopWhenFlagsDontMatch()
    {
        var vm = new GameViewModel(GameViewModel.Difficulty.Easy);
        vm.CellTapped(4, 4);
        if (NumberedRevealed(vm) is not (int r, int c))
        {
            Assert.Fail();
            return;
        }

        // Don't place flags. Snapshot grid (deep copy: Cell is a struct).
        var before = new Cell[vm.Grid.Length][];
        for (int rr = 0; rr < vm.Grid.Length; rr++)
        {
            before[rr] = (Cell[])vm.Grid[rr].Clone();
        }

        vm.Chord(r, c);

        for (int rr = 0; rr < vm.Grid.Length; rr++)
        {
            for (int cc = 0; cc < vm.Grid[rr].Length; cc++)
            {
                Assert.Equal(before[rr][cc], vm.Grid[rr][cc]);
            }
        }
    }

    [Fact]
    public void TestCustomBoardClampsAndPlays()
    {
        var vm = new GameViewModel();
        vm.SetCustom(5, 5, 3);
        Assert.Equal(GameViewModel.Difficulty.Custom, vm.CurrentDifficulty);
        Assert.Equal(5, vm.Rows);
        Assert.Equal(5, vm.Cols);
        Assert.Equal(3, vm.MineCount);
        vm.CellTapped(2, 2);
        Assert.Equal(3, TotalMines(vm));
        // The first tap is always safe, so the game must never be lost here.
        // (A tiny dense board can occasionally be cleared outright on the first
        // tap, so accept either Playing or Won — just not Lost.)
        Assert.NotEqual(GameState.Lost, vm.GameState);
    }

    [Fact]
    public void TestCustomBoardEnforcesMineUpperBound()
    {
        var vm = new GameViewModel();
        vm.SetCustom(5, 5, 100);
        Assert.Equal(GameViewModel.MaxMines(5, 5), vm.MineCount);
    }

    [Fact]
    public void TestCustomBoardEnforcesSideLimits()
    {
        var vm = new GameViewModel();
        vm.SetCustom(1, 999, 1);
        Assert.Equal(GameViewModel.MinBoardSide, vm.Rows);
        Assert.Equal(GameViewModel.MaxBoardSide, vm.Cols);
    }

    [Fact]
    public void TestResetGameClearsEverything()
    {
        var vm = new GameViewModel(GameViewModel.Difficulty.Easy);
        vm.CellTapped(0, 0);
        vm.CellFlagged(8, 8);
        vm.ResetGame();
        Assert.Equal(GameState.Playing, vm.GameState);
        Assert.Equal(0, vm.FlagsPlaced);
        Assert.Equal(0, vm.ElapsedTime);
        Assert.Equal(0, TotalMines(vm));
        Assert.Equal(0, RevealedCount(vm));
    }

    // MARK: helpers

    private static int TotalMines(GameViewModel vm)
    {
        int count = 0;
        for (int r = 0; r < vm.Rows; r++)
        {
            for (int c = 0; c < vm.Cols; c++)
            {
                if (vm.Grid[r][c].IsMine)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static int RevealedCount(GameViewModel vm)
    {
        int count = 0;
        for (int r = 0; r < vm.Grid.Length; r++)
        {
            for (int c = 0; c < vm.Grid[r].Length; c++)
            {
                if (vm.Grid[r][c].IsRevealed)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static (int, int)? FirstUnrevealed(GameViewModel vm)
    {
        for (int r = 0; r < vm.Rows; r++)
        {
            for (int c = 0; c < vm.Cols; c++)
            {
                if (!vm.Grid[r][c].IsRevealed)
                {
                    return (r, c);
                }
            }
        }

        return null;
    }

    private static (int, int)? FirstMine(GameViewModel vm)
    {
        for (int r = 0; r < vm.Rows; r++)
        {
            for (int c = 0; c < vm.Cols; c++)
            {
                if (vm.Grid[r][c].IsMine)
                {
                    return (r, c);
                }
            }
        }

        return null;
    }

    private static (int, int)? NumberedRevealed(GameViewModel vm)
    {
        for (int r = 0; r < vm.Rows; r++)
        {
            for (int c = 0; c < vm.Cols; c++)
            {
                Cell cell = vm.Grid[r][c];
                if (cell.IsRevealed && cell.NeighboringMines > 0)
                {
                    return (r, c);
                }
            }
        }

        return null;
    }
}
