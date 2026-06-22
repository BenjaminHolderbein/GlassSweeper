using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace GlassSweeper_App;

/// <summary>Persisted user settings and stats.</summary>
public sealed class GameSettings
{
    public string Difficulty { get; set; } = "Easy";
    public int CustomRows { get; set; } = 12;
    public int CustomCols { get; set; } = 12;
    public int CustomMines { get; set; } = 20;
    public bool Muted { get; set; }
    public int BestTime { get; set; }
    public int TotalWins { get; set; }
    public int TotalGames { get; set; }
}

/// <summary>
/// Lightweight JSON-file persistence under %LOCALAPPDATA%\GlassSweeper. Uses a
/// plain file (not ApplicationData) so it works identically packaged or not.
/// </summary>
public static class SettingsService
{
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GlassSweeper");

    private static readonly string FilePath = Path.Combine(Dir, "settings.json");

    public static GameSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(FilePath))
                    ?? new GameSettings();
            }
        }
        catch
        {
            // Corrupt or unreadable settings fall back to defaults.
        }

        return new GameSettings();
    }

    public static void Save(GameSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings));
        }
        catch
        {
            // Best-effort; never let a failed save crash the game.
        }
    }
}

/// <summary>
/// Win/loss feedback sounds. Plays bundled Windows .wav clips (chimes = win,
/// chord = loss) fire-and-forget via the winmm PlaySound API. Mute is gated by
/// the caller (the view model only calls these when unmuted).
/// </summary>
internal static class Sound
{
    private const uint SndAsync = 0x0001;      // play asynchronously, return immediately
    private const uint SndFilename = 0x00020000; // pszSound is a file path
    private const uint SndNoDefault = 0x0002;    // don't fall back to the default beep if missing

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern bool PlaySound(string? pszSound, IntPtr hmod, uint fdwSound);

    private static readonly string WinSound = AssetPath("chimes.wav");
    private static readonly string LossSound = AssetPath("chord.wav");

    public static void Win() => Play(WinSound);

    public static void Loss() => Play(LossSound);

    private static string AssetPath(string file) =>
        Path.Combine(AppContext.BaseDirectory, "Assets", file);

    private static void Play(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                PlaySound(path, IntPtr.Zero, SndFilename | SndAsync | SndNoDefault);
            }
        }
        catch
        {
            // No audio device / unsupported — silently ignore.
        }
    }
}
