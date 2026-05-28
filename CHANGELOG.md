# Deno Video Player - Changelog

Dates are KST (UTC+9).

## [0.4.0] - 2026-05-28

### Public Release Setup
- First public-ready release flow.
- GitHub Releases now produces a recommended `DenoVideoPlayer-win-Setup.exe`.
- Portable zip remains available for advanced users.
- Default update channel now points to the public `Deno2026/deno-video-player` releases.

### Installation
- First launch can prepare the mpv playback backend automatically, so Setup.exe
  users do not need to run `START_HERE.bat` manually.
- FFmpeg preparation is attempted in the background for the trim feature.
- Public packages exclude mpv/FFmpeg binaries and prepare them on the user's PC.

### Player UX
- Display/internal naming synchronized to `Deno Video Player` / `DenoVideoPlayer`.
- Native Windows caption buttons no longer reappear after fullscreen transitions.
- Fullscreen now settles into a clean video-only view shortly after entry.
- Top bar outline and left/right edge hints were polished for better visibility.
- Same-folder playlist, recent files panel, folder open, screenshot, playback speed
  presets, repeat/shuffle modes, and trim mode are included.

### Privacy
- No account, cloud sync, telemetry, analytics, ads, recommendations, or media
  library indexing.
- Update checks are opt-in for installation: the app only applies updates when the
  user clicks the update button.

### Verification
- `dotnet test .\DenoVideoPlayer.sln --configuration Release`: 65/65 passed.
- Published build was launched locally during the polish pass.

## Earlier Work

Earlier feature and UX work is preserved in Git history.
