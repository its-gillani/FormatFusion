using FormatFusion.Core.Models;

namespace FormatFusion.Core.Interfaces;


/// <summary>
/// Resolves the correct IFormatConverter for a given file extension.
/// Engines are registered at startup via DI; this acts as a lookup table.
/// </summary>
public interface IFormatRegistry
{
    /// <summary>
    /// Find an engine that can convert <paramref name="inputExtension"/>
    /// to <paramref name="outputExtension"/>.
    /// Returns null if no engine covers this pair.
    /// </summary>
    IFormatConverter? Resolve(string inputExtension, string outputExtension);

    /// <summary>All output formats available for a given input extension.</summary>
    IReadOnlyList<string> GetOutputFormats(string inputExtension);

    /// <summary>All input extensions handled by any registered engine.</summary>
    IReadOnlyList<string> GetAllSupportedInputExtensions();

    /// <summary>Detect the category (Image, Audio, Video, Document, Archive) for a file extension.</summary>
    FileCategory GetCategory(string extension);
}
