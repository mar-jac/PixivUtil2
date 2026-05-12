# PixivUtil Windows

PixivUtil Windows is the new Windows-native application foundation for PixivUtil.
It is built from scratch with modern C#/.NET and WinUI 3 instead of using Python
as the desktop UI technology.

## Stack

- .NET 10
- C# with nullable reference types
- WinUI 3 via Windows App SDK 2.0.1
- CommunityToolkit.Mvvm for typed MVVM state and commands
- Microsoft.Data.Sqlite 10.0.5 for existing account/download history
- HttpClient/System.Text.Json for Pixiv API preview calls

## Implemented workflows

- Followed-artist previews through Pixiv's `ajax/follow_latest/illust` endpoint.
- Account/download history from the existing `db.sqlite` tables:
  - Pixiv artworks
  - FANBOX posts
  - Sketch posts
- New content downloads from followed artists using the compatibility bridge for
  the existing feature-complete downloader.
- Combined workspace UI with:
  - screenshot-style gallery modes for Preview, New, Followed, Trending, and Popular
  - account/backend health
  - artist sidebar populated from the active feed for discovery
  - content-type and R-18 preview filters
  - search across history, artwork IDs, member IDs, and local paths
  - selected-preview downloads
  - selected-artist downloads
  - redownload from history
  - download queue state and backend output
  - CSV export for filtered history
- Full feature catalog for the legacy modes so parity is available while native
  services are implemented incrementally.

## Backend reliability goals

The Windows app separates backend-facing work into services so each piece can be
validated independently:

- `PixivApiClient` handles authenticated Pixiv JSON preview calls.
- `PixivDatabase` opens the existing SQLite database read-only for history.
- `PixivSettingsStore` reads and updates `config.ini` without changing unrelated
  keys.
- `LegacyPixivBridge` keeps every original downloader mode callable until that
  mode has a native C# implementation.
- `MainViewModel` tracks active jobs and command output so failures are visible
  instead of being swallowed by the UI.

Trending and Popular views are wired to Pixiv ranking JSON endpoints. Artist
discovery starts from the artists represented in those feeds: the user can open
the artist on Pixiv or download that artist's works through the existing member
download mode. A native in-app "follow this artist" action should be added only
after the authenticated Pixiv follow endpoint is verified against current Pixiv
CSRF/post-key requirements.

## Why a bridge still exists

The new app is not a Tkinter/Python GUI. It is a native Windows app. The bridge is
an explicit migration seam so the Windows UI can expose all current PixivUtil2
features immediately while C# services replace the old downloader internals mode
by mode. This avoids losing mature behavior such as FANBOX, Sketch, ugoira,
archive mode, metadata, blacklist handling, and database updates during the
rewrite.

## Build on Windows

Install the .NET 10 SDK and Windows App SDK build prerequisites, then run from the
repository root:

```powershell
.\windows\build-native.ps1
```

The publish output is written to:

```text
artifacts\PixivUtil.Windows\win-x64\
```

Use `-Runtime win-arm64` for ARM64 Windows devices.

## Roadmap for removing the bridge

1. Keep C# `PixivApiClient` responsible for authenticated JSON endpoint access.
2. Add native download pipelines for artwork files, manga pages, and ugoira.
3. Port database writes to typed repositories.
4. Port FANBOX and Sketch endpoint support.
5. Remove the bridge once native parity covers every mode in `DownloadMode.All`.
