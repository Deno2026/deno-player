# Deno Video Player — Changelog

날짜는 KST (UTC+9). 버전 태그는 Velopack release 시점에 부여한다.

---

## [Unreleased] — 2026-05-26 빌드

### Added
- **자동 업데이트 (opt-in pull)** — `UpdaterService`가 GitHub release channel을
  백그라운드로 확인. 새 버전이 있으면 좌상단 도구 막대에 **녹색 ☁⤓ 버튼**이
  활성화된다. 강제 안 함, 사용자 click 시에만 download + 적용 + 재시작.
  Default channel `github.com/Deno2026/deno-player-releases`, 환경변수
  `DENO_PLAYER_UPDATE_URL`로 override 가능.
- **환경설정 dialog (⚙)** — 탐색기 우클릭 "연결 프로그램"으로 등록할 확장자
  체크박스. 스크린샷 저장 폴더 picker. HKCU 4-tier (Applications + ProgID +
  Capabilities + RegisteredApplications) 등록 후 Windows "기본 앱 설정" 화면
  자동 오픈.
- **3-mode mutex repeat 버튼** — 기존 cycle 1-button 패턴을 폐기. 반복 없음 /
  전체 반복 / 한 곡 반복 각각 독립 button + DataTrigger green active state.
  현재 mode가 한눈에 보임. Shuffle button도 같은 패턴.
- **Space 길게 누르기 = 2배속** — YouTube 동일 timing (500ms hold 후 발동,
  release 시 원래 배속 복귀).
- **Slider 1:1 mapping drag** — SeekBar / Volume slider 모두 thumb 정확히
  안 잡아도 track 아무 위치 click + drag로 즉시 조절. Mouse capture 사용.
- **스크린샷 저장 폴더 사용자 지정** — `Settings.ScreenshotFolder` (비우면
  기본 `Pictures\Deno Video Player\`). 환경설정 dialog에서 picker.
- **하단 transport bar 영역 hot zone 제외** — 마우스를 transport bar 위에 둘
  때 hover 패널이 튀어나오지 않음. 좌·우 모두 세로 상반부에서만 trigger
  (대칭).
- **START_HERE.bat one-double-click install** — 압축 풀고 더블클릭 한 번으로
  mpv 자동 다운로드 + 바탕화면 아이콘 + 탐색기 연결 메뉴 등록 끝.
  `tools\7zr.exe` 번들 (LGPL, 602 KB) → 사용자가 별도 7-Zip 설치 불필요.

### Changed
- README에 신규 기능 (auto-update / 환경설정 / 스크린샷 폴더 / repeat·shuffle /
  settings.json 신규 필드) 반영. "의도적으로 없는 것"에서 `자동 업데이트` 제거.
- mpv mouse-pos 좌표계 의존 폐기 → Win32 `GetCursorPos` polling 80ms +
  WPF `CompositionTarget.TransformFromDevice` DPI 변환. HiDPI / 다중 모니터에서
  hot zone 안정.

### Fixed
- 환경설정 버튼이 안 먹던 버그 — `SettingsWindow.xaml`의 잘못된
  `StaticResource GroupAllCheckBox.HeaderText` 참조 제거 (XAML parse 시점에
  throw → 버튼 click이 silent fail로 보였음).
- SeekBar 중간 click + drag가 작동 안 하던 버그 — `IsMoveToPointEnabled=True`
  가 WPF Slider 내부 mouse capture를 가로채던 문제. False로 변경하고 우리가
  직접 capture + 1:1 mapping.
- PlaylistWindow 초기 렌더에서 항목이 보이지 않던 회귀 — owned child window의
  viewport가 layout 직전에 0인 시점에 VirtualizingStackPanel이 realize를 건너
  뛰는 문제. `IsVirtualizing=False`로 회피 (재생목록 항목 수 < 수백개 가정).

### Security / Privacy
- Telemetry · analytics · 계정 · 클라우드 sync · 광고 — **전부 없음** (코드 검색
  결과로도 hit 0).
- 자동 업데이트는 **opt-in pull** — 사용자가 직접 ☁⤓ 버튼 누를 때만 발동.
  background는 **확인만**, download / install은 절대 자동 수행 X.

### Verification
- `dotnet build -c Release`: 경고 0, 오류 0.
- `dotnet test -c Release`: 65/65 통과 (181 ms).
- Repeat None / RepeatAll wrap / RepeatOne (mpv loop-file=inf) / Shuffle
  (PickRandomOfSameKind) 모두 실제 재생 로그로 검증.
- Settings dialog → ScreenshotFolder 변경 → `settings.json` 저장 → 다음
  Ctrl+S 시 새 폴더로 저장 흐름 코드 경로 확인.

### Known / Pending (사용자 측)
- 자동 업데이트가 실제로 동작하려면 별도 public repo
  `Deno2026/deno-player-releases` 생성 + `dotnet tool install --global vpk` +
  `pwsh tools\pack-velopack.ps1`로 release 만들기.
- 코드 repo는 private 유지, release만 public repo에 publish하는 패턴 권장.

---

이전 작업은 git history에 있음 — `git log --oneline`로 확인 가능.
