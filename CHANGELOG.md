# Deno Video Player - Changelog

Dates are KST (UTC+9).

## [0.5.3] - 2026-08-02

### Player UX and responsiveness
- Replaced automatic left/right edge panels with explicit toolbar buttons and
  shortcuts. Recent files now use `Ctrl+H`; the playlist keeps `P` / `Ctrl+L`.
- Kept recent files and the playlist mutually exclusive so they cannot overlap
  in a narrow player window, and protected rapid close/reopen animations from
  applying stale layout work.
- Re-enabled recycling virtualization for recent and playlist rows so opening a
  panel does not construct every item in a large folder at once.

### Fullscreen and window behavior
- Keeps fullscreen above the taskbar while Deno Video Player is the foreground
  application, then releases that temporary priority when switching to another
  app.
- Applies the same effective always-on-top state to recent files, the playlist,
  and owned dialogs without changing the user's saved Always on top setting.

## [0.5.2] - 2026-07-24

### In-app guide
- Added an always-available `?` button and `F1` quick guide covering first use,
  controls, edge panels, shortcuts, tracks, screenshots, zoom/pan, clip export,
  and troubleshooting in Korean and English.
- Added contextual guide entry points to the empty player and playback-failure
  screen without interrupting first launch with a forced tour.
- Made the new guide and recovery actions keyboard reachable without global
  Space or Enter shortcuts intercepting focused buttons.

### Player UX and reliability
- Empty-player double-click now opens a file as advertised, while media
  double-click continues to toggle fullscreen. Double-click is ignored while
  media is loading, being dragged, or showing a playback failure.
- Restored standard maximize/restore behavior to the title bar and window
  button, leaving fullscreen to the media surface and playback control.
- Outside trim editing, `Esc` now leaves fullscreen in one press even when
  controls are hidden; during trim editing the first press safely cancels the edit.
- Improved muted-text contrast and corrected file-association guidance.
- Recent items on disconnected removable or network drives are retained instead
  of being permanently removed during startup cleanup.
- Rapid screenshots now reserve unique millisecond/suffix filenames instead of
  colliding within the same second.
- Restored the persisted `AutoPlayNext` behavior when repeat and shuffle are off.
- Settings confirmation now reports a real save failure instead of closing as
  if the change had been persisted.
- Playback-engine failures now have a dedicated Retry path, stay protected from
  stale play/next shortcuts, and reopen the current media after recovery.
- Playback-engine recovery now preserves the latest file requested at startup
  or through another app instance, and opens it after recovery instead of
  dropping the request.
- First-run, playback-engine, and export failures now keep their title, actions,
  and technical detail consistent in both Korean and English. Localized loading
  and failure details also refresh when the display language changes.

### Runtime and release safety
- Restricted legacy runtime discovery to app-owned locations and validates mpv
  before launch instead of searching arbitrary ancestor directories.
- Hardened IPC connect/disconnect cleanup so shutdown cannot publish a late
  connection and natural disconnects release their resources.
- Update packages are now downloaded only after the user accepts the update.
- Release notes are generated from the matching version section in this
  changelog, preventing a new release from reusing an older feature list.
- Release publishing now rebuilds from clean intermediates and rejects PDB or
  build-machine path leakage before packaging.
- Corrected the Windows compatibility manifest to declare Windows 10/11
  support; release packages remain win-x64.

## [0.5.1] - 2026-07-22

### Playlist UX
- Added a compact playlist sort menu with natural name, newest-first, and
  oldest-first ordering. Natural name order is the default.
- The selected order is restored on the next launch and is shared by playlist
  display, previous/next navigation, folder open, and folder drop.
- Reordering keeps the currently playing item selected and scrolls it back into
  view without reloading the media.

### Responsiveness and reliability
- Playlist enumeration and file-date reads now run away from the UI thread, so
  large, external, or network folders do not block panel interaction.
- Opening a folder now builds the first playable item and its same-kind playlist
  from one snapshot instead of scanning the folder twice.
- Rapid sort changes, overlapping file-open requests, and shutdown now discard
  stale background results instead of applying them to a newer playlist.
- Media and subtitle files dropped together remain part of the same open request,
  preventing a late subtitle command from attaching to the wrong media.

## [0.5.0] - 2026-07-20

### Player UX
- Corrected video zoom and pan scaling, including pan limits after window resize
  and aspect-ratio changes.
- Ctrl+wheel zoom now coalesces rapid input once per display frame and sends the
  zoom/pan properties as one ordered IPC batch, so direction changes and reset
  no longer wait behind stale transforms.
- Middle-button pan now follows explicit native/hook down, move, and up events
  across the mpv surface and window boundary without being canceled by a
  conflicting physical-button poll.
- Improved precision-wheel volume handling and secondary-monitor side-panel
  placement.
- Side panels now continue smoothly from their current position when hover
  direction changes, with faster distance-aware open and close motion and a
  shorter edge-intent delay.
- Side-panel windows are prepared only after the first frame, recent-file disk
  checks no longer run while a panel is opening, and ordinary mouse movement is
  filtered before it reaches the UI dispatcher.
- Clicking the video surface now activates an inactive player, and launching the
  app again restores the existing window even when no file was passed.
- Playlist visibility now follows the panel that is actually shown, and a
  double-click no longer reloads the same item twice.

### Trim and reliability
- Trim points, preview loops, duration, and seek state are cleared when moving to
  another media file.
- Exports are written to a temporary file and committed only after FFmpeg
  succeeds, so a failed export does not truncate an existing destination.
- Audio-only export now chooses an M4A-compatible output only when the source
  codec supports it, with MKA as the safe fallback.
- Closing or canceling an export now stops the FFmpeg process cleanly.

### Runtime and settings
- The main shell renders before cached-runtime startup work, normal launch no
  longer probes FFmpeg, and hidden ambient animations stop consuming render
  time while they are not visible or the player is inactive.
- Cached mpv uses a cheap normal-start check while retaining one full
  validation-and-repair attempt if process startup or IPC connection fails.
- Downloaded playback tools are SHA-256 verified, staged, version-checked, and
  promoted only after validation.
- Invalid runtime executables are detected instead of being accepted because a
  file merely exists.
- Corrupt settings files are preserved for recovery before safe defaults are
  loaded.
- Saving ordinary settings no longer opens Windows Default Apps automatically;
  that page remains available through its dedicated button.
- Legacy file-association identifiers continue to redirect to Deno Video Player.
- Portable builds can now detect a newer GitHub release and open its download
  page instead of silently skipping update checks.

### Packaging and licensing
- Added dedicated video, audio, and image file-association icons to public
  installer and portable packages.
- Hardened the tagged-release workflow with coverage artifacts, pinned build
  tooling, tag-format validation, and a main-branch ancestry gate.
- Changed the project license to `GPL-3.0-only` and retained third-party license
  and attribution details in `NOTICE.md`.

## [0.4.9] - 2026-05-31

### Fullscreen
- Unified fullscreen buttons, keyboard shortcuts, double-click handling, and
  restore behavior through one transition path.
- Improved restoration of the previous maximized or windowed bounds after
  leaving fullscreen.
- Made recent-files and playlist edge panels easier to reach in fullscreen
  while preserving a clean video-only view.

## [0.4.8] - 2026-05-30

### Updates
- New versions now show a clear update confirmation dialog with Update/Cancel
  instead of relying on a small top-bar icon.
- Automatic updates can use a local Velopack feed via `DENO_PLAYER_UPDATE_URL`,
  which keeps update-flow testing off the public release channel.

### Runtime
- mpv and FFmpeg are now kept in `%LOCALAPPDATA%\DenoVideoPlayer\runtime`
  instead of the versioned app folder, so app updates do not force the playback
  engine to download again.
- Existing per-version runtime files are promoted into the persistent runtime
  cache before applying an update when possible.

## [0.4.7] - 2026-05-30

### Fullscreen
- Fullscreen entry now keeps the interface available briefly, then force-hides
  the top and bottom controls after three seconds even if the mouse is resting
  over the controls.
- After that automatic fullscreen settle, a stationary mouse no longer
  immediately reopens the interface; moving the mouse or pressing a key brings
  the controls back.
- The left and right green edge hint strips are now collapsed in fullscreen so
  video-only mode has no side guide lines.

## [0.4.6] - 2026-05-30

### Player UX
- Side-panel edge hover now works when Deno Video Player is visible but inactive,
  so moving the mouse from another monitor onto the player can open the left or
  right panel without first clicking the app.
- Inactive hover is limited to cases where the cursor is actually over the
  player, its owned side panels, or the embedded mpv video surface, avoiding
  panel popups when another app covers the player.

## [0.4.5] - 2026-05-30

### Player UX
- Reduced accidental side-panel openings by matching the left and right windowed
  edge trigger width and adding a slightly longer hover intent delay.
- Restored fullscreen left/right edge panel access across the visible edge
  while still protecting the bottom transport strip.
- Re-synced hidden side-panel windows just before opening and after fullscreen
  animations, preventing stale panel positions after fullscreen transitions.
- Cursor polling now maps through the actual window rectangle and DPI transform
  for steadier fullscreen edge detection on maximized or high-DPI displays.

## [0.4.4] - 2026-05-28

### Fullscreen
- Made empty top-bar double-click handle fullscreen directly instead of relying
  on a later root double-click route that could be preempted by drag handling.
- The top-right maximize/restore button now shows restore while fullscreen and
  exits fullscreen before touching normal window maximize state.
- Cursor-poll double-click fallback now maps through the actual root visual
  instead of guessing maximized WorkArea coordinates.

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
