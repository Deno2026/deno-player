# Deno Video Player - Changelog

Dates are KST (UTC+9).

## Unreleased

## [0.4.3] - 2026-05-28

### Player UX
- Reduced right-edge playlist hover sensitivity and added open/close hysteresis
  so the panel does not flap while the mouse moves near the edge.
- Hidden side panels are now truly hidden instead of leaving transparent owned
  windows over the video surface.

## [0.4.2] - 2026-05-28

### Settings
- Fresh settings now select video and audio file associations by default, with
  image extensions left as an explicit opt-in.
- Clicking OK in Settings now saves the extension choices and immediately syncs
  them to the Windows "Open with" / Default apps candidate list.
- When extensions are selected, clicking OK now opens Windows Default Apps so
  the user can finish the protected double-click default choice immediately.

## [0.4.1] - 2026-05-28

### Player UX
- First launch now shows a dedicated playback-engine preparation screen instead
  of exposing a blank native video surface while mpv is being downloaded.
- The first-run preparation screen surfaces the current stage, hides transport
  controls until the player is ready, and returns to the normal empty state when
  no startup file was provided.
- Made fullscreen double-click toggling consistent across WPF chrome, blank
  controls space, and the native mpv video host.
- Blank bottom-bar/top-bar areas now follow the same double-click fullscreen rule,
  while actual controls such as buttons, sliders, lists, and text inputs are
  protected from accidental toggles.

### Updates
- Installed builds now check GitHub Releases periodically and prepare Velopack
  updates in the background.
- When an update is ready, the update button applies it by restarting the app;
  portable/dev runs skip automatic checks.

### Settings
- Added a display language setting with Korean and English options.
- Main screen, settings dialog, side panels, tooltips, dialogs, and common toast
  messages now follow the selected language.

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
