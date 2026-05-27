# Deno Player — Session Handoff

**Status (2026-05-26 KST 18시경):** PRIVATE repo. 사용자 피드백 받으면서 핵심 UX
다듬기 라운드 종료. main HEAD `e1e1961`. 65/65 unit tests pass, build 경고
0 / 오류 0. publish 폴더 (`publish/DenoPlayer-win-x64/`) 데스크탑 shortcut
가리키는 최신 binary 적용됨.

## Repository

- **Repo**: https://github.com/Deno2026/deno-player (**PRIVATE**)
- **Release channel (별도 public repo, 미생성)**: `Deno2026/deno-player-releases`
  — 사용자가 만들 차례 (자동 업데이트 활성화 시점에).
- **Tag**: 없음. 사용자가 일정 기간 써본 후 v0.4.0 release 결정.

## 이번 세션에서 완료한 일 — 큰 묶음

### 1. 단축키 / 패널 UX (`0dd1fad` ~ `5982011`)
- `Ctrl+←/→` = 이전/다음 파일 (`PageUp/PageDown` 외 추가, 동영상 관습)
- 좌/우 hover 패널 — TopBar 영역(상단 36px)에서는 이미 열린 패널 닫지 않음
  (`UpdateHotZones`에 `inTopBar` 검사)
- 패널 self-close 제거 (`PlaylistWindow.OnPanelLeave` / `RecentWindow.OnPanelLeave`)
  — 닫힘 판정 일원화 (MainWindow GetCursorPos 80ms polling 단일 경로)
- 패널 우클릭 ContextMenu 떠 있는 동안 패널 닫힘 방지
  — `_ctxMenuOpenCount` + `ContextMenuOpening/Closing` 라우티드 이벤트 bubble

### 2. 잘라내기 (Trim) — 핵심 기능 (`b25487f` 시작 → `e1e1961` 마무리)
ffmpeg stream copy(`-c copy`) 무손실 cut. ffmpeg.exe는
`runtime/ffmpeg/ffmpeg.exe`에 자동 다운로드 (`tools/fetch-ffmpeg.ps1`).

**최종 UX:**
- TopBar `✂ 가위` 버튼 → 편집 모드 토글 (진입/취소)
- 진입 시 mpv `ab-loop-a` / `ab-loop-b` set → **IN~OUT 구간 자동 무한 루프**
- SeekBar 위 overlay:
  - 회색 baseline (SeekBar 진행 가림)
  - IN/OUT pill 핸들 (8×26, accent green, soft glow, hit area는 18×34)
  - IN-OUT 사이 강조 (둥근 끝 rectangle)
  - 흰색 동그라미 PlaybackCursor (현재 재생 위치)
- 좌표 매핑: `SecToX` 함수로 SeekThumb (16px) track 영역 정확히 보정
- IN 핸들 드래그 시 그 위치로 live seek (들으면서 미세 조정)
  - `BeginSeek`/`EndSeek` 패턴으로 mpv 옛 time-pos 덮어쓰기 차단
- 하단 [💾 Save] 명시 버튼 → `SaveFileDialog` → 사용자가 위치/이름 지정
- 편집 모드 중 의미 없는 컨트롤 disable: Prev/Next/Repeat None/All/Shuffle.
  Repeat One은 시각만 accent green (편집 = 사실상 RepeatOne 의미라 일관)
- 가위 다시 / X / ESC → 편집 취소 (`ClearAbLoop`)

### 3. 타이틀바 더블클릭 = Fullscreen 토글 (`ff7e971` ~ `9f804c1`)
원래 더블클릭이 환경에 따라 안 먹는 회귀를 여러 fix 끝에 — **사용자 통찰**
("위치 무관, 현재 상태 기준") 받고 단순화:

- 어디서 더블클릭하든 (TopBar/영상/좌/우 빈 곳 다) **Fullscreen 토글**
- 컨트롤(Slider/Button/ListBox/BottomBar)만 제외
- WindowChrome `CaptionHeight=0` (OS NC 처리 없음, WPF가 100% input 받음)
- TopBar drag = deferred-drag 패턴 (`OnTopBarMouseDown` ClickCount=1만 arm,
  MouseMove threshold 넘으면 DragMove, ClickCount=2는 OnRootDoubleClick으로 위임)
- Maximize 토글은 TopBar의 □ 버튼만 (더블클릭 = fullscreen만)

### 4. Fullscreen 전환 부드럽게 (`5bc5821` ~ `8c5eaa3`)
- `AnimateBounds()` 헬퍼 — Left/Top/Width/Height 4개 DoubleAnimation 동시
  140ms cubic ease
- WindowChrome `SetWindowChrome` 호출 제거 (XAML 한 번 set으로 유지)
- WindowStyle.None + bounds 직접 set (모니터 전체)
- Win32 `MonitorFromWindow` + DPI 변환으로 현재 모니터 bounds 정확히
- `_savedWasMaximized` 플래그 — fullscreen 진입 전 Maximized였으면 탈출 시
  자동 Maximized 복원

### 5. 폴더 열기 + TopBar 아이콘 추가 (`3727141`)
- 빈 화면 (NoFile) — 「파일 열기」 옆에 「폴더 열기」 버튼
- TopBar에 📁 폴더 (E838) + ✂ 가위 (E8C6) 아이콘 추가
- `OpenFolderCommand` — `Microsoft.Win32.OpenFolderDialog` → 첫 미디어 파일

### 6. 재생속도 버튼 click → preset 메뉴 (`ad0fb81`)
- 우하단 `1.0x` 클릭 = ContextMenu 0.25/0.5/0.75/1.0/1.25/1.5/1.75/2.0/3.0/4.0
- `SetSpeedCommand` (`SetSpeedForce` 헬퍼 — Speed setter early-return 우회)
- 휠/Shift+./, 단축키 그대로

## 현재 main HEAD 최근 commit

```
e1e1961 편집 모드 UI 부드럽게 — pill 핸들 + 동그라미 cursor + 글로우
40e32d8 편집 모드: 의미 없는 컨트롤 disable (Prev/Next/Repeat/Shuffle)
887ccf1 편집 모드 UI 다듬기 — 핸들 시각 얇게 + 재생 cursor
b22ef57 편집 모드: mpv ab-loop로 IN~OUT 자동 루프
b872886 편집 모드 — SeekBar 진행 표시 가리고 IN/OUT만 노출
e1b4122 정지 상태 IN 드래그 fix v2 — Seeking flag
4001edb 정지 상태 IN 드래그 시각 반영 fix
301cd13 IN/OUT 핸들 좌표 어긋남 fix — Slider thumb track 보정
32369c1 가위는 항상 가위 유지 + 하단 명시적 Save 버튼
82d920c 자르기 UX 개선: 핸들 크게 + 단축키 안내 제거 + SaveFileDialog
69db77b 편집 모드 UX 명확화
bc2d53d IN 핸들 드래그 시 그 위치로 live seek
e62cb7e 자르기 편집 모드 — 가위 토글 + SeekBar IN/OUT 드래그 핸들
f2b0ad1 가위 버튼: IN/OUT 안 잡혀도 클릭 가능 + toast 안내
3727141 폴더 열기 + 자르기 트리거 아이콘을 TopBar에
8c5eaa3 Fullscreen 전환 220ms → 140ms
000cd4e Fullscreen 전환 200ms cubic ease 애니메이션
5bc5821 Fullscreen 전환 깜빡임 fix
9f804c1 더블클릭 = 어디서든 Fullscreen 토글 (위치 무관)
... (생략, 추가 30+ commits)
```

## 설치 (변경 없음)

```
1) zip 압축 풀기
2) START_HERE.bat 더블클릭
3) (한 번만) mpv + ffmpeg 자동 다운로드 + 바탕화면 바로가기 + 우클릭 메뉴 등록
```

제거: `UNINSTALL.bat` 더블클릭.

## 핵심 파일 / 구조

| 파일 | 역할 |
|---|---|
| `MainWindow.xaml` + `.xaml.cs` | 메인 UI + 핸들러. TopBar/SeekBar/BottomBar/편집 overlay |
| `ViewModels/MainViewModel.cs` | 모든 비즈니스 로직 (재생/재생목록/자르기/repeat/shuffle/update) |
| `Services/MpvProcessService.cs` | mpv 프로세스 실행/종료 + video host hwnd 연결 |
| `Services/MpvIpcClient.cs` | mpv JSON IPC over named pipe. `SetAbLoop`/`ClearAbLoop` 신규 |
| `Services/TrimService.cs` | ffmpeg `-c copy` 호출. `FindFfmpeg()`, `TrimAsync()` |
| `Services/UpdaterService.cs` | Velopack 자동 업데이트 (opt-in pull) |
| `Services/PlaylistService.cs` | 같은 폴더 미디어 자연 정렬 |
| `Services/FileAssociationService.cs` | HKCU 4-tier 연결 프로그램 등록 |
| `Views/PlaylistWindow.xaml(.cs)` | 우측 owned child window |
| `Views/RecentWindow.xaml(.cs)` | 좌측 owned child window |
| `Views/SettingsWindow.xaml(.cs)` | 환경설정 dialog (확장자/스크린샷 폴더) |
| `Views/ToastWindow.xaml(.cs)` | OSD toast (WPF airspace 우회 owned window) |
| `Models/AppSettings.cs` | settings.json 모델. ScreenshotFolder/repeatMode/shuffle 등 |
| `Helpers/RelayCommand.cs` | ICommand 구현 (CommandManager.RequerySuggested hook) |
| `Helpers/TimeFormat.cs` | `Seconds(double)` → `HH:MM:SS` |
| `Themes/DesignTokens.xaml` | 색/spacing/font/style 단일 source |
| `tools/fetch-mpv.ps1` | mpv 자동 다운 (zhongfly/mpv-winbuild) |
| `tools/fetch-ffmpeg.ps1` | ffmpeg 자동 다운 (BtbN/FFmpeg-Builds win64 LGPL) |
| `tools/install.ps1` | HKCU 등록 + 바탕화면 / 시작 메뉴 바로가기 |
| `tools/7zr.exe` | 7-Zip standalone (LGPL, 602KB) — mpv 7z extract |
| `tools/pack-velopack.ps1` | Velopack release 패키징 |
| `START_HERE.bat` | 더블클릭 한 번 설치 (mpv + ffmpeg + install.ps1) |
| `UNINSTALL.bat` | 더블클릭 제거 |

## settings.json (`%APPDATA%\DenoPlayer\settings.json`)

```jsonc
{
  "windowWidth": 1280, "windowHeight": 760,
  "windowLeft": 100, "windowTop": 60,
  "windowMaximized": false,
  "volume": 100, "muted": false, "playbackRate": 1.0,
  "lastOpenedFolder": "...",
  "autoPlayNext": true,
  "controlAutoHideMs": 2500,
  "playlistPanelEnabled": true,
  "alwaysOnTop": false,
  "recentFiles": [],
  "repeatMode": "None",   // None / All / One
  "shuffle": false,
  "screenshotFolder": null,  // null = Pictures\DenoPlayer\
  "enabledExtensions": [".mp4", ".mkv", ...]
}
```

## 단축키 (현재 라운드 기준)

| 키 | 동작 |
|---|---|
| `Space` | 재생/일시정지 (hold 500ms = 2x 배속) |
| `←` / `→` | 5초 |
| `Shift + ← / →` | 30초 |
| `↑` / `↓` | 볼륨 ±5% |
| `M` | 음소거 |
| `F` / `F11` / `Enter` / `Alt+Enter` | Fullscreen |
| `Esc` | Fullscreen exit (편집 모드면 편집 cancel) |
| `PageUp` / `PageDown` | 이전/다음 파일 |
| `Ctrl + ← / →` | 이전/다음 파일 |
| `Shift + . / ,` | 배속 ±0.25 |
| `. / ,` | 프레임 step / back step |
| `Ctrl + O` | 파일 열기 |
| `Ctrl + S` | 스크린샷 |
| `Ctrl + T` | 항상 위 |
| `P` / `Ctrl + L` | 재생목록 패널 |
| `V` / `Shift + V` | 자막 사이클 / 표시 토글 |
| `Ctrl + J` | 오디오 트랙 사이클 |
| `I` / `O` / `X` / `Ctrl + E` | 자르기 IN/OUT/Clear/Execute (UI에 안내 없음) |
| 더블클릭 (어디서든) | Fullscreen 토글 |
| 휠 | 볼륨 |

## Pending (사용자 측)

1. **자동 업데이트 채널 활성화** — `Deno2026/deno-player-releases` public repo
   생성 + `dotnet tool install --global vpk` + `pwsh tools\pack-velopack.ps1`.
2. **첫 release tag** (v0.4.0 추천) — 사용자가 실제로 써본 후 결정.

## 의도적으로 안 한 / 안 하기로 결정한 것

- I/O/X/Ctrl+E 단축키는 코드엔 있지만 UI에 안내 X — 사용자가 "보통 사용자는
  단축키 안 씀" → hint 패널/ToolTip에서 단축키 텍스트 제거.
- 자르기 OUT 핸들 드래그 시 live seek X — IN만 함 (OUT 시점은 직전이라
  미리듣기 의미 작음). 필요하면 OUT-1.5초 seek 추가 가능.
- 편집 모드에서 Prev/Next/Repeat/Shuffle disabled — ab-loop가 IN~OUT 반복하니
  의미 없음. Repeat One만 시각 활성 (편집 = 사실상 RepeatOne 의미).
- Fullscreen 진입 후 Maximized였으면 자동 복원 (`_savedWasMaximized`).

## 자율 진행 가드 (다음 세션 참고)

- private origin/main push는 사용자 명시 승인 받음 — 자유 push OK.
- Velopack publish (public release repo로) / tag bump / public 노출은 **사용자
  명시 승인 후만**.
- 사용자가 화면 보고 피드백하는 작업은 매 turn 후 `taskkill /F /IM DenoPlayer.exe`
  → `dotnet publish` 권한 받음. publish 폴더 갱신해야 desktop shortcut 다음
  실행 시 새 binary 적용.

## 알려진 / 잠재 이슈

- **PlaylistWindow 초기 렌더 회귀** (이전 세션 기록): GetCursorPos polling으로
  `ShowSlide()` 정상 호출 + position 정상 trace 확인. screenshot에 contents가
  안 보였던 건 dev 환경 (DPI / screenshot capture artifact) 의심.
  `IsVirtualizing=False`는 들어가 있음.
- **자르기 stream copy 정확도** — `-c copy` 사용으로 키프레임 단위. IN 위치가
  0~수 초 어긋날 수 있음. 정밀 편집기 아님 (의도).
- **ffmpeg 165MB** — git에 포함 안 됨 (`.gitignore`에 `runtime/ffmpeg/`).
  사용자별 `START_HERE.bat` 또는 `fetch-ffmpeg.ps1`로 받기. publish 폴더에는
  복사함.

## CI

- main / PR push마다 `build` job (test 포함).
- `v*` tag push 시 `release` job — 현재 tag 없음. 사용자 결정 후 진행.

— Claude Code (Opus 4.7)
