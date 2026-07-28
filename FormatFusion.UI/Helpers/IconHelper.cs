using FormatFusion.Core;

namespace FormatFusion.UI.Helpers;

public static class IconHelper
{
    public static string GetIcon(FileCategory cat) => cat switch
    {
        FileCategory.Image => "M21 19V5a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2z M21 15l-5-5L5 21 M10 8.5a1.5 1.5 0 1 1-3 0 1.5 1.5 0 0 1 3 0z",
        FileCategory.Audio => "M9 18V5l12-2v13 M6 15a3 3 0 1 0 3 3v-3H6z M18 13a3 3 0 1 0 3 3v-3h-3z",
        FileCategory.Video => "M19.8 19.8V4.2a2 2 0 0 0-2-2H6.2a2 2 0 0 0-2 2v15.6a2 2 0 0 0 2 2h11.6a2 2 0 0 0 2-2z M7 2v20 M17 2v20 M2 12h20 M2 7h5 M2 17h5 M17 17h5 M17 7h5",
        FileCategory.Document => "M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z M14 2v6h6 M16 13H8 M16 17H8 M10 9H8",
        FileCategory.Archive => "M21 8v13H3V8 M1 3h22v5H1z M10 12h4v8h-4z",
        _ => "M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z M14 2v6h6"
    };
}
