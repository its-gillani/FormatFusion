using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace FormatFusion.Core.Services;

public class AppSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FormatFusion", "settings.json");

    public string DefaultOutputFolder { get; set; }
    public bool OpenFolderAfterCompletion { get; set; } = true;
    public int MaxConcurrentJobs { get; set; } = 2;
    public string FfmpegPath { get; set; } = "Bundled";
    public string PandocPath { get; set; } = "Auto-detect";
    public bool ClearTempOnExit { get; set; } = true;

    // Hardware Acceleration
    public string HardwareBackend { get; set; } = "Auto (Recommended)"; // Auto (Recommended), CPU, Intel GPU, AMD GPU, NVIDIA GPU
    public ObservableCollection<string> AvailableBackends { get; } = new() { "Auto (Recommended)", "CPU" };
    public HardwareCapabilities? HardwareCaps { get; private set; }

    // Video Codec
    public string VideoCodec { get; set; } = "Default"; // Default, H.264, H.265, VP9, AV1
    public ObservableCollection<string> AvailableCodecs { get; } = new() { "Default", "H.264", "H.265", "VP9", "AV1" };

    // Window State
    public double WindowWidth { get; set; } = 950;
    public double WindowHeight { get; set; } = 650;
    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop { get; set; } = double.NaN;
    public bool WindowMaximized { get; set; } = false;

    public event Action? SettingsChanged;

    public AppSettings()
    {
        DefaultOutputFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads", "FormatFusion_Output");
        Load();
    }

    public void Save()
    {
        var data = new
        {
            DefaultOutputFolder,
            OpenFolderAfterCompletion,
            MaxConcurrentJobs,
            FfmpegPath,
            PandocPath,
            ClearTempOnExit,
            HardwareBackend,
            VideoCodec,
            WindowWidth,
            WindowHeight,
            WindowLeft,
            WindowTop,
            WindowMaximized
        };
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var options = new JsonSerializerOptions { WriteIndented = true, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(data, options));
        SettingsChanged?.Invoke();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(SettingsPath));
            var root = doc.RootElement;
            if (root.TryGetProperty("DefaultOutputFolder", out var v)) DefaultOutputFolder = v.GetString() ?? DefaultOutputFolder;
            if (root.TryGetProperty("OpenFolderAfterCompletion", out var v2)) OpenFolderAfterCompletion = v2.GetBoolean();
            if (root.TryGetProperty("MaxConcurrentJobs", out var v3)) MaxConcurrentJobs = v3.GetInt32();
            if (root.TryGetProperty("ClearTempOnExit", out var v4)) ClearTempOnExit = v4.GetBoolean();
            if (root.TryGetProperty("HardwareBackend", out var v5)) HardwareBackend = v5.GetString() ?? "Auto (Recommended)";
            if (root.TryGetProperty("VideoCodec", out var v6)) VideoCodec = v6.GetString() ?? "Default";
            if (root.TryGetProperty("WindowWidth", out var w)) WindowWidth = GetDoubleSafe(w);
            if (root.TryGetProperty("WindowHeight", out var h)) WindowHeight = GetDoubleSafe(h);
            if (root.TryGetProperty("WindowLeft", out var l)) WindowLeft = GetDoubleSafe(l);
            if (root.TryGetProperty("WindowTop", out var t)) WindowTop = GetDoubleSafe(t);
            if (root.TryGetProperty("WindowMaximized", out var m)) WindowMaximized = m.GetBoolean();
        }
        catch { /* Corrupt settings — use defaults */ }
    }

    private static double GetDoubleSafe(System.Text.Json.JsonElement el)
    {
        if (el.ValueKind == System.Text.Json.JsonValueKind.Number) return el.GetDouble();
        if (el.ValueKind == System.Text.Json.JsonValueKind.String && el.GetString() == "NaN") return double.NaN;
        return double.NaN;
    }

    public async Task InitializeHardwareDetectionAsync(string ffmpegExePath, Action<Action>? dispatcherInvoke = null)
    {
        var detector = new HardwareDetectionService();
        HardwareCaps = await detector.DetectCapabilitiesAsync(ffmpegExePath);

        var addAction = new Action(() =>
        {
            if (HardwareCaps.NvidiaUsable && !AvailableBackends.Contains("NVIDIA GPU")) AvailableBackends.Add("NVIDIA GPU");
            if (HardwareCaps.AmdUsable && !AvailableBackends.Contains("AMD GPU")) AvailableBackends.Add("AMD GPU");
            if (HardwareCaps.IntelUsable && !AvailableBackends.Contains("Intel GPU")) AvailableBackends.Add("Intel GPU");
        });

        if (dispatcherInvoke != null)
            dispatcherInvoke(addAction);
        else
            addAction();

        // If the saved backend isn't available, fallback to Auto
        if (!AvailableBackends.Contains(HardwareBackend))
        {
            HardwareBackend = "Auto (Recommended)";
            Save();
        }
    }
}
