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

/// <summary>Win/loss feedback sounds, mirroring SwiftSweeper's chimes.</summary>
internal static class Sound
{
    private const uint MbIconAsterisk = 0x00000040; // pleasant chime — win
    private const uint MbIconHand = 0x00000010;      // error tone — loss

    [DllImport("user32.dll")]
    private static extern bool MessageBeep(uint uType);

    public static void Win() => Beep(MbIconAsterisk);

    public static void Loss() => Beep(MbIconHand);

    private static void Beep(uint type)
    {
        try
        {
            MessageBeep(type);
        }
        catch
        {
            // No audio device / unsupported — silently ignore.
        }
    }
}
