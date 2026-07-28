# Third-Party Licenses

## FFmpeg
FormatFusion bundles fmpeg.exe and fprobe.exe for media processing capabilities.

**License:** GNU General Public License v3.0 (GPLv3)

### Legal & Licensing Rationale
The bundled FFmpeg binaries are "full" standard builds (e.g., from gyan.dev or BtbN) that include GPL-licensed components, notably libx264 and libx265. 
libx265 is strictly licensed under the GPLv2 (or a commercial license) and has no LGPL equivalent. Because FFmpeg is linked against these GPL libraries, the entire fmpeg.exe binary falls under the GPL.

**Is FormatFusion infected by the GPL?**
No. FormatFusion utilizes FFmpeg as a separate, unmodified standalone executable. It spawns fmpeg.exe out-of-process via standard command-line arguments and parses its stdout/stderr streams. It does **not** statically or dynamically link to any FFmpeg libraries (like libavcodec.so or vcodec.dll).

Under the Free Software Foundation's (FSF) FAQ regarding the GPL, this constitutes "mere aggregation" and communicating "at arm's length." As long as the two programs are separate and communicate via standard OS mechanisms (like pipes or command-line arguments), the GPL wrapper of FFmpeg does not extend to the FormatFusion application itself.

**Redistribution Compliance:**
To comply with the GPL distribution terms for the FFmpeg binary:
1. This notice informs users that the bundled FFmpeg is GPL software.
2. The FFmpeg source code (and the specific configuration flags used to compile the bundled binary) can be obtained from the original upstream provider (e.g., https://www.gyan.dev/ffmpeg/builds/ or https://github.com/BtbN/FFmpeg-Builds).

## Magick.NET
FormatFusion utilizes Magick.NET for image processing operations.

**License:** Apache License 2.0
Magick.NET is linked as a standard NuGet package. The Apache 2.0 license permits commercial and proprietary use, provided the license and copyright notices are preserved.

## Ookii.Dialogs.Wpf
FormatFusion utilizes Ookii.Dialogs.Wpf for native Windows folder selection dialogs.

**License:** BSD 3-Clause License
This component is linked as a standard NuGet package and is fully compatible with proprietary or commercial software distribution.
