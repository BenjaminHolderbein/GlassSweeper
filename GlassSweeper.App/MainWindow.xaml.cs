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

        // Lock the window to its content size: the board game should always hug
        // the board, so disable user resizing and maximizing. Programmatic
        // resizing (on difficulty change) still works.
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;

            // Keep the window's minimum below the smallest board so the
            // content-hugging resize is never clamped by a default minimum.
            presenter.PreferredMinimumWidth = 120;
            presenter.PreferredMinimumHeight = 120;
        }

        // Navigate the root frame to the main page on startup.
        RootFrame.Navigate(typeof(MainPage));
    }

    /// <summary>
    /// Resizes the window's client area to the given size (in DIPs), but only
    /// when it differs from the <em>actual current</em> client size — so
    /// re-fitting an already-correct board is a no-op and never moves the
    /// window. Comparing against reality (not the last request) is important:
    /// an early request made while the window is still settling may not land at
    /// the requested size, and the reactive fit must be able to correct it once
    /// the window has settled. Returns <c>true</c> if a resize happened.
    /// </summary>
    public bool SetClientSize(double widthDip, double heightDip)
    {
        FrameworkElement? root = Content as FrameworkElement;
        double scale = root?.XamlRoot?.RasterizationScale ?? 1.0;

        int w = (int)Math.Ceiling(widthDip * scale);
        int h = (int)Math.Ceiling(heightDip * scale);

        SizeInt32 current = AppWindow.ClientSize;
        if (Math.Abs(w - current.Width) <= 1 && Math.Abs(h - current.Height) <= 1)
        {
            return false; // Already the right size — don't resize.
        }

        AppWindow.ResizeClient(new SizeInt32(w, h));
        return true;
    }

    /// <summary>Current client width in DIPs.</summary>
    public double ClientWidthDip
    {
        get
        {
            FrameworkElement? root = Content as FrameworkElement;
            double scale = root?.XamlRoot?.RasterizationScale ?? 1.0;
            return AppWindow.ClientSize.Width / scale;
        }
    }

    /// <summary>Current client height (page area + title bar) in DIPs.</summary>
    public double ClientHeightDip
    {
        get
        {
            FrameworkElement? root = Content as FrameworkElement;
            double scale = root?.XamlRoot?.RasterizationScale ?? 1.0;
            return AppWindow.ClientSize.Height / scale;
        }
    }

    public void Center()
    {
        DisplayArea area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
        RectInt32 work = area.WorkArea;
        int x = work.X + ((work.Width - AppWindow.Size.Width) / 2);
        int y = work.Y + ((work.Height - AppWindow.Size.Height) / 2);
        AppWindow.Move(new PointInt32(Math.Max(work.X, x), Math.Max(work.Y, y)));
    }
}
