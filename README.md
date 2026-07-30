# FormatFusion

**One offline Windows app. Every conversion. Smart compression.**

FormatFusion is a high-performance, completely offline utility for Windows that seamlessly handles file conversions and intelligent compression across video, audio, image, document, and archive formats. Built for a pragmatic, polished, and shippable solo developer experience, it brings together the power of industry-standard CLI tools into a beautiful, unified graphical interface.

Zero uploads. Zero subscriptions. 100% local processing.

If you ever find FormatFusion helpful, please consider giving this repo a star on github. The support of the community motivates me to craft such useful apps.
---
FormatFusion's Hardware Acceleration has not been widely tested, only testing on AMD GPU has been carried out by myself and Intel iGPU has been tested too by the co-developer of this app and even that testing was not done on a large scale, since it was a small project handled by myself, therefore the app is to have many bugs yet and thus i would appreciate users of the app to notify me in case they run into any bugs/issues across the app and i'll resolve them ASAP.

## Features

- **Universal Format Support:** Convert between lots of video and photo formats seamlessly.
- **Smart Compression:** Specify a target output size (e.g., "Compress to 25MB") and FormatFusion handles the complex bitrate math and multipass encoding automatically via an intelligent binary-search loop.
- **Hardware Acceleration:** Native support for NVIDIA (NVENC), AMD (AMF), and Intel (QSV) hardware encoders, significantly speeding up video processing.
- **Pre-flight Conflict Detection:** Proactively detects impossible codec-container combinations (e.g., VP9 inside MP4) and prompts for resolution before starting the job.
- **Modern Fluent UI:** Built with WPF and ModernWpf, featuring dark/light modes, drag-and-drop support, and smooth micro-animations.
- **Privacy First:** Completely offline. Your files never leave your computer.

---

## Prerequisites

If you are running the pre-compiled installers, you do not need to install any external dependencies (FFmpeg, FFprobe, and Pandoc are bundled). 

If you wish to build the app from source, you will need:
- **OS:** Windows 10 (Version 2004, Build 19041) or later (64-bit)
- **SDK:** [.NET 9.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- **IDE:** Visual Studio 2022 (v17.12+) or JetBrains Rider or VS Code with C# Dev Kit.

---

## Hardware Acceleration Options
- Auto: Uses default GPU on the device, and fallbacks to CPU if no GPU has been found or selected conversion is not possible on the GPU installed in your system.
- CPU: Uses CPU only for conversion/compression, it is not recommended to be used, especially not for videos.
- AMD GPU: For both AMD GPU and AMD iGPU
- Intel GPU: For both Intel GPU and Intel iGPU
- NVIDIA GPU: For NVIDIA GPUs

## Installation

You can install FormatFusion either by using the provided pre-compiled installers or by building it manually from source.

### Option A: Using the Installers (Recommended)

Pre-built installers are available in two formats:

1. **Classic Setup (`.exe`)**
   - The `FormatFusion-Setup-X.X.X.exe` is a standard Windows installer powered by Inno Setup. 
   - It is highly compressed, fast, and easy to use.

2. **Windows Installer (`.msi`)**
   - The `FormatFusion-X.X.X.msi` is an enterprise-friendly Windows Installer package. 
   - **Requirement:** The `.msi` installer explicitly requires 64-bit Windows 10, version 2004 (Build 19041) or later. 

Both installers are fully self-contained and require no additional runtime installations.

### Option B: Manual Build (For Developers)

To build the application manually from the source code:

1. **Clone the repository:**
   ```bash
   git clone https://github.com/YourUsername/FormatFusion.git
   cd FormatFusion
   ```

2. **Restore dependencies:**
   ```bash
   dotnet restore
   ```

3. **Build the application:**
   ```bash
   dotnet build --configuration Release
   ```

4. **Run the application:**
   ```bash
   dotnet run --project FormatFusion.UI --configuration Release
   ```

*(Note: The UI project requires `ffmpeg.exe`, `ffprobe.exe`, and `pandoc.exe` to be present in the `FormatFusion.UI/Tools` directory to function fully.)*

---

## Usage

1. **Launch the App:** Open FormatFusion from your Start menu or desktop shortcut.
2. **Select an Action:**
   - **Convert Format:** Change the format of a file (e.g., MP4 → MKV, JPG → PNG).
   - **Convert Codec:** Change the underlying codec of a video (e.g., H.264 → H.265/HEVC).
   - **Compress:** Shrink a video or photo to a specific target file size in MB.
3. **Add Files:** Drag and drop files onto the designated area or click to browse.
4. **Configure Options:** Select your desired output format, codec, or target file size. Adjust hardware acceleration settings via the Settings page if needed.
5. **Start:** Click "Convert Now" or "Compress Now" and monitor the progress in the Queue tab.

---

## License

FormatFusion is open-sourced under the MIT License. See the [LICENSE](LICENSE) file for more details.

---

## Acknowledgments

FormatFusion stands on the shoulders of giants. It utilizes several incredible open-source projects, including:
- [FFmpeg](https://ffmpeg.org/) (via FFMpegCore)
- [ImageMagick](https://imagemagick.org/) (via Magick.NET)
- [Pandoc](https://pandoc.org/)
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)
- [ModernWpf](https://github.com/Kinnara/ModernWpf)
