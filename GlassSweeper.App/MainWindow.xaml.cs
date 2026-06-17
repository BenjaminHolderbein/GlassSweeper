using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GlassSweeper_App;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");

        // Navigate the root frame to the main page on startup.
        RootFrame.Navigate(typeof(MainPage));
    }

    /// <summary>
    /// Sizes the window so it hugs the board (plus the title bar, HUD, and
    /// padding) rather than floating in a large window, and centers it on the
    /// current display. Called whenever the board is (re)built.
    /// </summary>
    public void ResizeToBoard(int rows, int cols)
    {
        // Layout constants — must mirror MainPage.xaml and MainPage cell sizing.
        const double stride = 30;        // 28pt cell + 2pt gap
        const double boardChrome = 26;   // nested board borders + padding
        const double outerPad = 12;      // StackPanel padding on each side
        const double spacing = 12;       // gap between top bar / HUD / board
        const double topBar = 32;        // top bar band height
        const double hud = 40;           // HUD band height
        const double minContentWidth = 250;

        FrameworkElement? root = Content as FrameworkElement;
        double scale = root?.XamlRoot?.RasterizationScale ?? 1.0;
        double titleBar = AppTitleBar.ActualHeight > 0 ? AppTitleBar.ActualHeight : 48;

        double boardW = (cols * stride) + boardChrome;
        double boardH = (rows * stride) + boardChrome;
        double clientWidth = Math.Max(boardW, minContentWidth) + (outerPad * 2);
        double clientHeight = titleBar + (outerPad * 2) + topBar + spacing + hud + spacing + boardH;

        int w = (int)Math.Ceiling(clientWidth * scale);
        int h = (int)Math.Ceiling(clientHeight * scale);

        AppWindow.ResizeClient(new SizeInt32(w, h));

        // Center on the work area of the display the window is on.
        DisplayArea area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
        RectInt32 work = area.WorkArea;
        int x = work.X + ((work.Width - AppWindow.Size.Width) / 2);
        int y = work.Y + ((work.Height - AppWindow.Size.Height) / 2);
        AppWindow.Move(new PointInt32(Math.Max(work.X, x), Math.Max(work.Y, y)));
    }
}
