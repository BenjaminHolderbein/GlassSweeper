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

    private int _sizedWidth;
    private int _sizedHeight;

    /// <summary>
    /// Sizes the window's client area to fit the measured content (plus the
    /// title bar) and centers it — but only when the size actually changes, so
    /// starting a new game on the same board doesn't move the window.
    /// </summary>
    public void SizeToContent(double contentWidthDip, double contentHeightDip)
    {
        FrameworkElement? root = Content as FrameworkElement;
        double scale = root?.XamlRoot?.RasterizationScale ?? 1.0;
        double titleBar = AppTitleBar.ActualHeight > 0 ? AppTitleBar.ActualHeight : 48;

        int w = (int)Math.Ceiling(contentWidthDip * scale);
        int h = (int)Math.Ceiling((contentHeightDip + titleBar) * scale);

        if (w == _sizedWidth && h == _sizedHeight)
        {
            return; // Already the right size — don't resize or re-center.
        }

        _sizedWidth = w;
        _sizedHeight = h;

        AppWindow.ResizeClient(new SizeInt32(w, h));

        // Center on the work area of the display the window is on.
        DisplayArea area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
        RectInt32 work = area.WorkArea;
        int x = work.X + ((work.Width - AppWindow.Size.Width) / 2);
        int y = work.Y + ((work.Height - AppWindow.Size.Height) / 2);
        AppWindow.Move(new PointInt32(Math.Max(work.X, x), Math.Max(work.Y, y)));
    }
}
