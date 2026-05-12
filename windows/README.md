# Windows builds

## Modern native app

The preferred Windows path is the new C#/.NET WinUI app under
`src/PixivUtil.Windows`. It is built from scratch as a modern Windows-native
frontend with typed services for previews, account history, and followed-artist
download orchestration.

Build it on Windows with:

```powershell
.\windows\build-native.ps1
```

The output is published to `artifacts\PixivUtil.Windows\win-x64\`.

## Legacy compatibility bundle

This directory contains the maintained Windows-native packaging path for PixivUtil2.
It does not reimplement the downloader. Instead, it ships the existing feature-complete
Python core as Windows executables:

- `PixivUtil2.exe` - console application with the full interactive and command-line
  feature set.
- `PixivUtilGUI.exe` - Tkinter GUI wrapper that launches the bundled console
  executable and writes `config.ini` beside the bundle.

Keeping both executables in the same one-folder bundle preserves the existing
download handlers, FANBOX/Sketch flows, database management, config migration,
templates, browser-cookie support, and FFmpeg-based ugoira conversion behavior.

## Build requirements

- Windows 10 or newer.
- Python 3.11+ from <https://www.python.org/>.
- PowerShell 5+.
- Optional: `ffmpeg.exe` if the release should work with ugoira conversion without
  requiring users to configure FFmpeg separately.

## Build command

From the repository root on Windows:

```powershell
.\windows\build.ps1
```

To include FFmpeg in the bundle:

```powershell
.\windows\build.ps1 -FfmpegPath C:\Tools\ffmpeg\bin\ffmpeg.exe
```

The script creates `.venv-windows`, installs the runtime dependencies plus
PyInstaller, runs the test suite, builds the one-folder bundle, and creates:

```text
dist\PixivUtil2-Windows\
dist\PixivUtil2-Windows.zip
```

Use `-SkipTests` for a local packaging-only build and `-NoZip` when an installer
pipeline will consume the folder directly.

## Manual PyInstaller command

If you already manage the virtual environment yourself:

```powershell
python -m pip install -r requirements.txt "pyinstaller>=6.20.0"
$env:PIXIVUTIL_FFMPEG = "C:\Tools\ffmpeg\bin\ffmpeg.exe" # optional
python -m PyInstaller --clean --noconfirm windows\PixivUtil2-Windows.spec
```

## Bundle layout

The generated folder contains both executables and the packaged Python runtime.
The first run creates or updates `config.ini` in the bundle folder, matching the
existing frozen-app path behavior in `common/PixivHelper.py`.

Important runtime files included by the spec:

- `template.html` and `novel_template.html`
- `content_provider.json`
- `requirements.txt` and `README.md`
- certifi CA data
- cloudscraper/browser-cookie/curl-cffi package data
- optional `ffmpeg.exe` via `-FfmpegPath`

## Release checklist

1. Build on a clean Windows machine or VM.
2. Launch `PixivUtil2.exe --help`.
3. Launch `PixivUtilGUI.exe` and confirm it can start `PixivUtil2.exe`.
4. Test cookie login using a throwaway `config.ini`.
5. If FFmpeg is bundled, enable an ugoira conversion option and confirm
   conversion works without a system PATH entry.
6. Sign the executables or wrap `dist\PixivUtil2-Windows` with your preferred
   installer technology, such as WiX, Inno Setup, NSIS, or MSIX.

## Notes

- XMP writing still requires the optional `pyexiv2` runtime and its Windows
  Visual C++ runtime dependencies.
- IrfanView integration remains optional and is controlled by existing
  `config.ini` settings.
- The console executable remains the source of truth for all downloader features;
  the GUI should continue to call it rather than duplicate handler logic.
