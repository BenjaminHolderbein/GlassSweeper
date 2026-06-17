using GlassSweeper.Core;
using GlassSweeper_App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace GlassSweeper_App;

/// <summary>
/// The game board. Cells are lightweight <see cref="Border"/> elements built in
/// code so the grid can resize per difficulty. Pointer input is routed directly
/// per cell — no application-level event monitor needed.
/// </summary>
public sealed partial class MainPage : Page
{
    private const double CellSize = 40;

    private readonly SolidColorBrush _tileBrush = new(Color.FromArgb(0x55, 0x8A, 0x9B, 0xB5));
    private readonly SolidColorBrush _tileHoverBrush = new(Color.FromArgb(0x88, 0xA8, 0xBC, 0xD8));
    private readonly SolidColorBrush _tileBorder = new(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF));
    private readonly SolidColorBrush _revealedBrush = new(Color.FromArgb(0x26, 0x00, 0x00, 0x00));
    private readonly SolidColorBrush _explodedBrush = new(Color.FromArgb(0xDD, 0xE0, 0x55, 0x61));
    private readonly SolidColorBrush _defaultText = new(Color.FromArgb(0xFF, 0xF2, 0xF4, 0xF8));
    private readonly SolidColorBrush _questionBrush = new(Color.FromArgb(0xFF, 0xF0, 0xC0, 0x60));
    private readonly SolidColorBrush[] _numberBrushes;

    private Border[,]? _cells;

    public MainPage()
    {
        InitializeComponent();

        // Classic minesweeper number palette (index 1..8), tuned for a dark glass board.
        _numberBrushes = new SolidColorBrush[9];
        _numberBrushes[1] = new SolidColorBrush(Color.FromArgb(0xFF, 0x5B, 0x8C, 0xFF)); // blue
        _numberBrushes[2] = new SolidColorBrush(Color.FromArgb(0xFF, 0x3F, 0xC3, 0x6B)); // green
        _numberBrushes[3] = new SolidColorBrush(Color.FromArgb(0xFF, 0xF0, 0x5F, 0x6B)); // red
        _numberBrushes[4] = new SolidColorBrush(Color.FromArgb(0xFF, 0xB6, 0x7B, 0xF0)); // purple
        _numberBrushes[5] = new SolidColorBrush(Color.FromArgb(0xFF, 0xF0, 0xA0, 0x4B)); // orange
        _numberBrushes[6] = new SolidColorBrush(Color.FromArgb(0xFF, 0x2C, 0xC8, 0xC0)); // teal
        _numberBrushes[7] = new SolidColorBrush(Color.FromArgb(0xFF, 0xE0, 0xE4, 0xEC)); // near-white
        _numberBrushes[8] = new SolidColorBrush(Color.FromArgb(0xFF, 0x9A, 0xA4, 0xB2)); // grey

        ViewModel.BoardReset += (_, _) => BuildBoard();
        ViewModel.BoardChanged += (_, _) => RefreshBoard();
        Loaded += (_, _) => BuildBoard();
    }

    public GameBoardViewModel ViewModel { get; } = new();

    /// <summary>x:Bind helper — converts the game-over flag to a Visibility.</summary>
    public Visibility BoolToVisibility(bool value) =>
        value ? Visibility.Visible : Visibility.Collapsed;

    private void BuildBoard()
    {
        int rows = ViewModel.Rows;
        int cols = ViewModel.Cols;

        BoardHost.Children.Clear();
        BoardHost.RowDefinitions.Clear();
        BoardHost.ColumnDefinitions.Clear();

        for (int r = 0; r < rows; r++)
        {
            BoardHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(CellSize) });
        }

        for (int c = 0; c < cols; c++)
        {
            BoardHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CellSize) });
        }

        _cells = new Border[rows, cols];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var text = new TextBlock
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 22,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                };
                var border = new Border
                {
                    Margin = new Thickness(1.5),
                    CornerRadius = new CornerRadius(6),
                    Child = text,
                    Tag = (r, c),
                };
                border.PointerPressed += Cell_PointerPressed;
                border.PointerEntered += Cell_PointerEntered;
                border.PointerExited += Cell_PointerExited;
                Grid.SetRow(border, r);
                Grid.SetColumn(border, c);
                BoardHost.Children.Add(border);
                _cells[r, c] = border;
            }
        }

        RefreshBoard();

        // Snap the window to fit the new board dimensions.
        if (App.Window is MainWindow window)
        {
            window.ResizeToBoard(rows, cols);
        }
    }

    private void RefreshBoard()
    {
        if (_cells is null)
        {
            return;
        }

        Cell[][] grid = ViewModel.Game.Grid;
        int rows = ViewModel.Game.Rows;
        int cols = ViewModel.Game.Cols;

        // Guard against a transient mismatch between a difficulty change and the
        // grid rebuild (BoardChanged can fire before BoardReset rebuilds the cells).
        if (_cells.GetLength(0) != rows || _cells.GetLength(1) != cols)
        {
            return;
        }

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                StyleCell(_cells[r, c], grid[r][c]);
            }
        }
    }

    private void StyleCell(Border border, Cell cell)
    {
        var text = (TextBlock)border.Child;

        if (!cell.IsRevealed)
        {
            border.Background = _tileBrush;
            border.BorderBrush = _tileBorder;
            border.BorderThickness = new Thickness(1);

            if (cell.Mark == CellMark.Flag)
            {
                text.Text = "\U0001F6A9"; // 🚩
                text.Foreground = _defaultText;
            }
            else if (cell.Mark == CellMark.Question)
            {
                text.Text = "?";
                text.Foreground = _questionBrush;
            }
            else
            {
                text.Text = string.Empty;
            }

            return;
        }

        border.BorderThickness = new Thickness(0);

        if (cell.IsMine)
        {
            border.Background = cell.IsExploded ? _explodedBrush : _revealedBrush;
            text.Text = "\U0001F4A3"; // 💣
            text.Foreground = _defaultText;
            return;
        }

        border.Background = _revealedBrush;
        int n = cell.NeighboringMines;
        if (n > 0)
        {
            text.Text = n.ToString();
            text.Foreground = _numberBrushes[n];
        }
        else
        {
            text.Text = string.Empty;
        }
    }

    private void Cell_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel.IsGameOver)
        {
            return;
        }

        var border = (Border)sender;
        (int row, int col) = ((int, int))border.Tag;
        Microsoft.UI.Input.PointerPointProperties p = e.GetCurrentPoint(border).Properties;

        if (p.IsRightButtonPressed)
        {
            ViewModel.Flag(row, col);
        }
        else if (p.IsMiddleButtonPressed)
        {
            ViewModel.Chord(row, col);
        }
        else if (p.IsLeftButtonPressed)
        {
            // Left-clicking an already-revealed number chords; otherwise reveal.
            if (ViewModel.Game.Grid[row][col].IsRevealed)
            {
                ViewModel.Chord(row, col);
            }
            else
            {
                ViewModel.Reveal(row, col);
            }
        }

        e.Handled = true;
    }

    private void Cell_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        var border = (Border)sender;
        (int row, int col) = ((int, int))border.Tag;
        if (!ViewModel.Game.Grid[row][col].IsRevealed)
        {
            border.Background = _tileHoverBrush;
        }
    }

    private void Cell_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        var border = (Border)sender;
        (int row, int col) = ((int, int))border.Tag;
        if (!ViewModel.Game.Grid[row][col].IsRevealed)
        {
            border.Background = _tileBrush;
        }
    }

    private void OnFaceClicked(object sender, RoutedEventArgs e) => ViewModel.NewGame();

    private void OnDifficultyEasy(object sender, RoutedEventArgs e) =>
        ViewModel.ApplyDifficulty(GameViewModel.Difficulty.Easy);

    private void OnDifficultyMedium(object sender, RoutedEventArgs e) =>
        ViewModel.ApplyDifficulty(GameViewModel.Difficulty.Medium);

    private void OnDifficultyHard(object sender, RoutedEventArgs e) =>
        ViewModel.ApplyDifficulty(GameViewModel.Difficulty.Hard);

    private async void OnDifficultyCustom(object sender, RoutedEventArgs e)
    {
        var rowsBox = new NumberBox { Header = "Rows", Value = ViewModel.Game.CustomRows, Minimum = GameViewModel.MinBoardSide, Maximum = GameViewModel.MaxBoardSide, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        var colsBox = new NumberBox { Header = "Columns", Value = ViewModel.Game.CustomCols, Minimum = GameViewModel.MinBoardSide, Maximum = GameViewModel.MaxBoardSide, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        var minesBox = new NumberBox { Header = "Mines", Value = ViewModel.Game.CustomMines, Minimum = 1, Maximum = GameViewModel.MaxMines(GameViewModel.MaxBoardSide, GameViewModel.MaxBoardSide), SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(rowsBox);
        panel.Children.Add(colsBox);
        panel.Children.Add(minesBox);

        var dialog = new ContentDialog
        {
            Title = "Custom board",
            Content = panel,
            PrimaryButtonText = "Start",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            int rows = (int)(double.IsNaN(rowsBox.Value) ? ViewModel.Game.CustomRows : rowsBox.Value);
            int cols = (int)(double.IsNaN(colsBox.Value) ? ViewModel.Game.CustomCols : colsBox.Value);
            int mines = (int)(double.IsNaN(minesBox.Value) ? ViewModel.Game.CustomMines : minesBox.Value);
            ViewModel.ApplyCustom(rows, cols, mines);
        }
    }
}
