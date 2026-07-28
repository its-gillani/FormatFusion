Place the following binaries in this directory:

1. ffmpeg.exe   — FFmpeg LGPL essentials build
                  Download: https://www.gyan.dev/ffmpeg/builds/ffmpeg-git-essentials.7z
                  (Extract and copy ffmpeg.exe from the bin/ folder)

2. ffprobe.exe  — Bundled with FFmpeg (in same bin/ folder)

3. pandoc.exe   — Optional, for document conversion
                  Download: https://pandoc.org/installing.html
                  (Windows installer places pandoc.exe in PATH;
                   alternatively copy it here manually)

These binaries are NOT included in the repository.
They must be sourced and placed here before running the app.

LICENSE NOTES:
- FFmpeg: LGPL 2.1+ (essentials build only — no GPL codecs)
- Pandoc: GPL 2.0+ (external process, not linked into this app)
