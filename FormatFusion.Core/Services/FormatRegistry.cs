using FormatFusion.Core.Interfaces;

namespace FormatFusion.Core.Services;

/// <summary>
/// Resolves IFormatConverter engines by extension pair.
/// Engines are registered at DI startup; this class is purely a lookup table.
/// </summary>
public sealed class FormatRegistry : IFormatRegistry
{
    // extension (lowercased, with dot) → engine
    private readonly Dictionary<string, IFormatConverter> _inputMap = new();
    private readonly Dictionary<string, FileCategory> _categoryMap = new()
    {
        { ".jpg", FileCategory.Image }, { ".jpeg", FileCategory.Image },
        { ".png", FileCategory.Image }, { ".webp", FileCategory.Image },
        { ".heic", FileCategory.Image }, { ".heif", FileCategory.Image },
        { ".bmp", FileCategory.Image }, { ".gif", FileCategory.Image },
        { ".tiff", FileCategory.Image }, { ".tif", FileCategory.Image },
        { ".ico", FileCategory.Image }, { ".cr2", FileCategory.Image },
        { ".cr3", FileCategory.Image }, { ".nef", FileCategory.Image },
        { ".arw", FileCategory.Image }, { ".dng", FileCategory.Image },

        { ".mp3", FileCategory.Audio }, { ".wav", FileCategory.Audio },
        { ".flac", FileCategory.Audio }, { ".aac", FileCategory.Audio },
        { ".ogg", FileCategory.Audio }, { ".opus", FileCategory.Audio },
        { ".m4a", FileCategory.Audio },

        { ".mp4", FileCategory.Video }, { ".mkv", FileCategory.Video },
        { ".avi", FileCategory.Video }, { ".mov", FileCategory.Video },
        { ".webm", FileCategory.Video }, { ".flv", FileCategory.Video },
        { ".wmv", FileCategory.Video }, { ".3gp", FileCategory.Video },

        { ".pdf", FileCategory.Document }, { ".docx", FileCategory.Document },
        { ".txt", FileCategory.Document }, { ".rtf", FileCategory.Document },
        { ".odt", FileCategory.Document }, { ".epub", FileCategory.Document },
        { ".html", FileCategory.Document }, { ".md", FileCategory.Document },

        { ".zip", FileCategory.Archive }, { ".7z", FileCategory.Archive },
        { ".rar", FileCategory.Archive }, { ".tar", FileCategory.Archive },
        { ".gz", FileCategory.Archive }, { ".bz2", FileCategory.Archive },
        { ".xz", FileCategory.Archive }
    };

    /// <summary>Register all engines at startup via DI.</summary>
    public void Register(IFormatConverter engine)
    {
        foreach (var ext in engine.SupportedInputExtensions)
            _inputMap[ext.ToLowerInvariant()] = engine;
    }

    public IFormatConverter? Resolve(string inputExtension, string outputExtension)
    {
        var ext = inputExtension.ToLowerInvariant();
        if (!_inputMap.TryGetValue(ext, out var engine)) return null;
        return engine.SupportedOutputExtensions.Contains(outputExtension.ToLowerInvariant())
            ? engine : null;
    }

    public IReadOnlyList<string> GetOutputFormats(string inputExtension)
    {
        var ext = inputExtension.ToLowerInvariant();
        return _inputMap.TryGetValue(ext, out var engine)
            ? engine.SupportedOutputExtensions
            : Array.Empty<string>();
    }

    public IReadOnlyList<string> GetAllSupportedInputExtensions()
        => _inputMap.Keys.ToList();

    public FileCategory GetCategory(string extension)
    {
        var ext = extension.ToLowerInvariant();
        if (!ext.StartsWith('.')) ext = "." + ext;
        return _categoryMap.TryGetValue(ext, out var cat) ? cat : FileCategory.Unknown;
    }
}
