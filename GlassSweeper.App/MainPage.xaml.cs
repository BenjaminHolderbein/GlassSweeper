using GlassSweeper.Core;
using GlassSweeper_App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using Windows.UI;

namespace GlassSweeper_App;

/// <summary>
/// The game board. Cells are lightweight <see cref="Border"/> elements built in
/// code so the grid resizes per difficulty. Pointer input is routed per cell;
/// full keyboard play is handled at the page level with a keyboard-only focus
/// ring (no hover highlight), matching SwiftSweeper.
/// </summary>
public sealed partial class MainPage : Page
{
    private const double CellSize = 28;
    private const double CellGap = 2;
    private const double CellStride = CellSize + CellGap;

    private readonly SolidColorBrush _tileBrush = new(Color.FromArgb(0x2E, 0xFF, 0xFF, 0xFF));
    private readonly SolidColorBrush _tileBorder = new(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
    private readonly SolidColorBrush _revealedBrush = new(Color.FromArgb(0x73, 0x00, 0x00, 0x00));
    private readonly SolidColorBrush _explodedBrush = new(Color.FromArgb(0x99, 0xE0, 0x55, 0x61));
    private readonly SolidColorBrush _defaultText = new(Color.FromArgb(0xFF, 0xF2, 0xF4, 0xF8));
    private readonly SolidColorBrush _questionBrush = new(Color.FromArgb(0xFF, 0xF0, 0xC0, 0x60));
    private readonly SolidColorBrush _transparent = new(Color.FromArgb(0x00, 0x00, 0x00, 0x00));
    private readonly Brush _focusBrush;
    private readonly SolidColorBrush[] _numberBrushes;

    private Border[,]? _cells;
    private int _focusedRow;
    private int _focusedCol;
    private bool _usingKeyboard;

    public MainPage()
    {
        InitializeComponent();

        // SwiftSweeper's dark-mode number palette (index 1..8).
        _numberBrushes = new SolidColorBrush[9];
        _numberBrushes[1] = Rgb(0.40, 0.64, 1.00);
        _numberBrushes[2] = Rgb(0.30, 0.82, 0.35);
        _numberBrushes[3] = Rgb(1.00, 0.30, 0.30);
        _numberBrushes[4] = Rgb(0.20, 0.40, 0.85);
        _numberBrushes[5] = Rgb(0.67, 0.27, 0.27);
        _numberBrushes[6] = Rgb(0.30, 0.82, 0.82);
        _numberBrushes[7] = Rgb(1.00, 1.00, 1.00);
        _numberBrushes[8] = Rgb(0.55, 0.55, 0.55);

        _focusBrush = Application.Current.Resources.TryGetValue("AccentFillColorDefaultBrush", out object? b) && b is Brush accent
            ? accent
            : new SolidColorBrush(Color.FromArgb(0xFF, 0x5B, 0x8C, 0xFF));

        ViewModel.BoardReset += (_, _) => BuildBoard();
        ViewModel.BoardChanged += (_, _) => RefreshBoard();
        Loaded += OnLoaded;
    }

    public GameBoardViewModel ViewModel { get; } = new();

    /// <summary>x:Bind helper — converts a bool to a Visibility.</summary>
    public Visibility BoolToVisibility(bool value) =>
        value ? Visibility.Visible : Visibility.Collapsed;

    private static SolidColorBrush Rgb(double r, double g, double b) =>
        new(Color.FromArgb(0xFF, (byte)(r * 255), (byte)(g * 255), (byte)(b * 255)));

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildBoard();
        LayoutRoot.Focus(FocusState.Programmatic);
    }

    private void BuildBoard()
    {
        int rows = ViewModel.Rows;
        int cols = ViewModel.Cols;

        BoardHost.Children.Clear();
        BoardHost.RowDefinitions.Clear();
        BoardHost.ColumnDefinitions.Clear();

        for (int r = 0; r < rows; r++)
        {
            BoardHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(CellStride) });
        }

        for (int c = 0; c < cols; c++)
        {
            BoardHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CellStride) });
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
                    FontSize = CellSize * 0.58,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                };
                var border = new Border
                {
                    Margin = new Thickness(CellGap / 2),
                    CornerRadius = new CornerRadius(4),
                    Child = text,
                    Tag = (r, c),
                };
                border.PointerPressed += Cell_PointerPressed;
                Grid.SetRow(border, r);
                Grid.SetColumn(border, c);
                BoardHost.Children.Add(border);
                _cells[r, c] = border;
            }
        }

        // Center the keyboard cursor on the new board.
        _focusedRow = Math.Min(rows / 2, rows - 1);
        _focusedCol = Math.Min(cols / 2, cols - 1);

        RefreshBoard();
        LayoutRoot.Focus(FocusState.Programmatic);
        ResizeWindowToContent();
    }

    private void ResizeWindowToContent()
    {
        if (App.Window is not MainWindow window)
        {
            return;
        }

        // Measure the content at its natural size so the window fits it exactly
        // (no leftover bottom "chin").
        ContentRoot.UpdateLayout();
        ContentRoot.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        Windows.Foundation.Size desired = ContentRoot.DesiredSize;

        // The game-over card needs a minimum width; without this, a small board
        // would make the window narrower than the card and clip the stat values.
        const double minWidthForOverlay = 264;
        double width = Math.Max(desired.Width, minWidthForOverlay);
        window.SizeToContent(width, desired.Height);
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

        if (_cells.GetLength(0) != rows || _cells.GetLength(1) != cols)
        {
            return;
        }

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                StyleCell(_cells[r, c], grid[r][c], r, c);
            }
        }
    }

    private void StyleCell(Border border, Cell cell, int row, int col)
    {
        var text = (TextBlock)border.Child;
        bool focused = _usingKeyboard && row == _focusedRow && col == _focusedCol;

        if (!cell.IsRevealed)
        {
            border.Background = _tileBrush;
            border.BorderBrush = focused ? _focusBrush : _tileBorder;
            border.BorderThickness = new Thickness(focused ? 2 : 1);

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
        }
        else
        {
            border.BorderBrush = focused ? _focusBrush : _transparent;
            border.BorderThickness = new Thickness(focused ? 2 : 0);

            if (cell.IsMine)
            {
                border.Background = cell.IsExploded ? _explodedBrush : _revealedBrush;
                text.Text = "\U0001F4A3"; // 💣
                text.Foreground = _defaultText;
            }
            else
            {
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
        }

        AutomationProperties.SetName(border, DescribeCell(cell, row, col));
    }

    private static string DescribeCell(Cell cell, int row, int col)
    {
        string pos = $"Row {row + 1}, column {col + 1}";
        if (!cell.IsRevealed)
        {
            return cell.Mark switch
            {
                CellMark.Flag => $"{pos}, flagged",
                CellMark.Question => $"{pos}, question mark",
                _ => $"{pos}, hidden",
            };
        }

        if (cell.IsMine)
        {
            return cell.IsExploded ? $"{pos}, exploded mine" : $"{pos}, mine";
        }

        return cell.NeighboringMines > 0 ? $"{pos}, {cell.NeighboringMines}" : $"{pos}, empty";
    }

    private void Cell_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_usingKeyboard)
        {
            _usingKeyboard = false;
            RefreshBoard();
        }

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

        // Keep keyboard focus on the board so arrows/space/F keep working
        // after a mouse click.
        LayoutRoot.Focus(FocusState.Programmatic);
    }

    private void OnLayoutKeyDown(object sender, KeyRoutedEventArgs e)
    {
        int rows = ViewModel.Rows;
        int cols = ViewModel.Cols;
        bool handled = true;

        switch (e.Key)
        {
            case VirtualKey.Up:
                MoveCursor(Math.Max(0, _focusedRow - 1), _focusedCol);
                break;
            case VirtualKey.Down:
                MoveCursor(Math.Min(rows - 1, _focusedRow + 1), _focusedCol);
                break;
            case VirtualKey.Left:
                MoveCursor(_focusedRow, Math.Max(0, _focusedCol - 1));
                break;
            case VirtualKey.Right:
                MoveCursor(_focusedRow, Math.Min(cols - 1, _focusedCol + 1));
                break;
            case VirtualKey.Space:
                _usingKeyboard = true;
                ViewModel.Reveal(_focusedRow, _focusedCol);
                break;
            case VirtualKey.F:
                _usingKeyboard = true;
                ViewModel.Flag(_focusedRow, _focusedCol);
                break;
            case VirtualKey.Enter:
                _usingKeyboard = true;
                if (ViewModel.IsGameOver)
                {
                    ViewModel.NewGame();
                }
                else
                {
                    ViewModel.Chord(_focusedRow, _focusedCol);
                }

                break;
            default:
                handled = false;
                break;
        }

        e.Handled = handled;
    }

    private void MoveCursor(int row, int col)
    {
        _focusedRow = row;
        _focusedCol = col;
        _usingKeyboard = true;
        RefreshBoard();
    }

    // ---- HUD / menu handlers ----
    private void OnFaceClicked(object sender, RoutedEventArgs e) => ViewModel.NewGame();

    private void OnMuteClicked(object sender, RoutedEventArgs e) => ViewModel.ToggleMute();

    private void OnPillTapped(object sender, TappedRoutedEventArgs e) => ViewModel.ToggleGameOverCollapse();

    private void OnCollapseOverlay(object sender, RoutedEventArgs e) => ViewModel.ToggleGameOverCollapse();

    private void OnDifficultyEasy(object sender, RoutedEventArgs e) =>
        ViewModel.ApplyDifficulty(GameViewModel.Difficulty.Easy);

    private void OnDifficultyMedium(object sender, RoutedEventArgs e) =>
        ViewModel.ApplyDifficulty(GameViewModel.Difficulty.Medium);

    private void OnDifficultyHard(object sender, RoutedEventArgs e) =>
        ViewModel.ApplyDifficulty(GameViewModel.Difficulty.Hard);

    private async void OnDifficultyCustom(object sender, RoutedEventArgs e) => await ShowCustomDialogAsync();

    // ---- Keyboard accelerators (Ctrl+...) ----
    private void OnAccelNewGame(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        ViewModel.NewGame();
        args.Handled = true;
    }

    private void OnAccelEasy(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        ViewModel.ApplyDifficulty(GameViewModel.Difficulty.Easy);
        args.Handled = true;
    }

    private void OnAccelMedium(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        ViewModel.ApplyDifficulty(GameViewModel.Difficulty.Medium);
        args.Handled = true;
    }

    private void OnAccelHard(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        ViewModel.ApplyDifficulty(GameViewModel.Difficulty.Hard);
        args.Handled = true;
    }

    private async void OnAccelCustom(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ShowCustomDialogAsync();
    }

    private void OnAccelMute(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        ViewModel.ToggleMute();
        args.Handled = true;
    }

    private void OnAccelPeek(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (ViewModel.IsGameOver)
        {
            ViewModel.ToggleGameOverCollapse();
        }

        args.Handled = true;
    }

    private async System.Threading.Tasks.Task ShowCustomDialogAsync()
    {
        var rowsBox = new NumberBox { Header = "Rows", Value = ViewModel.CustomRows, Minimum = GameViewModel.MinBoardSide, Maximum = GameViewModel.MaxBoardSide, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        var colsBox = new NumberBox { Header = "Columns", Value = ViewModel.CustomCols, Minimum = GameViewModel.MinBoardSide, Maximum = GameViewModel.MaxBoardSide, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        var minesBox = new NumberBox { Header = "Mines", Value = ViewModel.CustomMines, Minimum = 1, Maximum = GameViewModel.MaxMines(GameViewModel.MaxBoardSide, GameViewModel.MaxBoardSide), SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };

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
            int r = (int)(double.IsNaN(rowsBox.Value) ? ViewModel.CustomRows : rowsBox.Value);
            int c = (int)(double.IsNaN(colsBox.Value) ? ViewModel.CustomCols : colsBox.Value);
            int m = (int)(double.IsNaN(minesBox.Value) ? ViewModel.CustomMines : minesBox.Value);
            ViewModel.ApplyCustom(r, c, m);
        }
    }
}
