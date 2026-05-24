# Deno Player — Session Handoff

**Status (2026-05-24, v0.1.4):** Popup 제거 + Grid 레이아웃 fix 후, 코드
정독으로 잠재 버그 일제 점검·수정. 사용자 손 테스트 대기.

## 이번 라운드(잠재 버그 점검) — 수정 내역

| 영역 | 증상/위험 | 수정 |
|---|---|---|
| **App.OnAppStartup**에서 명령줄 인자 처리 + **OnSourceInit**에서도 처리 | 같은 파일 두 번 LoadFile, 첫 번째는 IPC 미연결로 swallow | App 쪽 호출 제거. OnSourceInit이 유일한 진입점 |
| **PlayMedia 자동 재생** | 일시정지 상태에서 PageDown → 다음 곡도 일시정지로 시작 (mpv가 pause 유지) | `_ipc.SetPause(false)` 명시 + `IsPaused = false` |
| **재생목록 자동 스크롤** | 큰 폴더에서 현재 항목이 화면 밖이면 안 보임 | `CurrentMedia` 변경 시 `PlaylistListBox.ScrollIntoView` |
| **ShowControls 폭주** | mpv `mouse-pos`는 픽셀당 보고 → 매번 `BeginAnimation` | `_osdShown` 플래그로 dedup |
| **mpv hwnd 0 가드** | `BuildWindowCore` 미완 시 mpv가 자체 윈도우 띄움 | hwnd 0이면 Failed 상태로 명확히 알림 |
| **mpv crash 자동 복구** | mpv가 죽으면 사용자는 앱 재시작해야 함 | 5초 간격 + 최대 3회까지 자동 재시작, 이전 파일 재로드 |
| **OpenPath IPC 미연결 가드** | mpv 죽은 상태에서 파일 열면 조용히 실패 | "mpv 백엔드 연결이 끊겼습니다" 메시지 |
| **Disconnect 핸들러가 Failed 상태 덮어쓰기** | mpv crash → Failed → Disconnect → NoFile로 덮임 | Failed면 보존 |
| **OnRootDoubleClick이 TopBar 영역에서도 풀스크린 토글** | 더블클릭 시 max/restore + 풀스크린이 동시에 | TopBar/BottomBar 영역 제외 |
| **ListBox ↑↓ 키가 볼륨 단축키 가로챔** | ListBox에 focus 가면 우리 KeyBinding 안 통함 | `KeyboardNavigation.DirectionalNavigation=None` + `Focusable=False` |
| **Single instance** | 여러 영상 더블클릭 시 N개 프로세스 + N개 mpv | Mutex + named pipe → 두 번째는 첫 인스턴스에 인자 보내고 종료, 첫 인스턴스가 새 파일 열고 활성화 |

## 어디까지 했나

| 영역 | 상태 |
|---|---|
| 프로젝트 스캐폴드 (.NET 8 WPF, net8.0-windows, x64) | ✅ |
| `Themes/DesignTokens.xaml` 단일 디자인 출처 | ✅ |
| Services: Mpv 프로세스/IPC + Playlist + Settings + **SingleInstance** | ✅ |
| Helpers: `Win32VideoHost` (child HWND), 자연 정렬, 변환기, RelayCommand | ✅ |
| ViewModel: state, commands, mpv property 양방향, MouseActivity, **IsPlaylistOpen** | ✅ |
| MainWindow: **Grid 레이아웃** (Popup 제거), WindowChrome captionless, 풀스크린 | ✅ |
| 단축키, drag&drop, 명령줄 인자, **single-instance 활성화/인자 전달** | ✅ |
| **mpv crash 자동 복구** (5s 간격 / 3회) | ✅ |
| `tools/fetch-mpv.ps1` (검증됨 v0.41.0-689) | ✅ |
| `tools/install.ps1` (HKCU 등록 + Desktop/StartMenu) | ✅ 사용자 PC 등록 완료 |
| README / LICENSE / NOTICE / .gitignore / .gitattributes | ✅ |
| `.github/workflows/build.yml` | ✅ |
| `dotnet publish -c Release -r win-x64 --self-contained false` | ✅ `publish/DenoPlayer-win-x64/` |
| smoke test (3s 기동, single-instance, app+mpv alive, 정상 종료) | ✅ |
| 시각 동작 검증 (NoFile/Failed/풀스크린, 영상 재생, 자동 스크롤) | ⏳ 사용자 손 테스트 |
| GitHub push | ⛔ 사용자 명시 승인 전 금지 |

## 알려진 trade-off (의도된 결정)

1. **컨트롤이 영상 위에 오버레이되지 않음.** Popup이 다른 앱 위에 떠
   사용자 작업을 방해하던 문제를 해결하기 위해 Grid 레이아웃으로 전환.
   영상 영역이 TopBar/BottomBar/PlaylistPanel 면적만큼 작아짐.
   풀스크린(`F`)에서는 컨트롤 자동 숨김으로 영상 풀 화면.
   복원하려면 owned-window 방식이 필요한데 그건 별도 라운드.
2. **재생목록 슬라이드 hover 제거 → 명시적 토글(`P` 키 / 우상단 📋).**
   우측 끝 hover hot-zone은 Popup이 다시 필요해서 제거.
3. **이미지 자동 다음 이동 없음.** `image-display-duration=inf`로 유지,
   사용자가 PageDown으로 직접 전환.
4. **Volume slider TwoWay binding으로 픽셀당 mpv 명령 발생.** mpv는
   안정적으로 처리하므로 throttle 안 함.
5. **풀스크린 진입/탈출 시 WindowChrome을 null로 토글.** 한 프레임
   깜빡일 수 있음. 흔한 패턴.

## 즉시 사용자가 할 일

**기존 인스턴스가 떠 있다면 제가 죽였습니다.** 바탕화면의 "Deno Player"
아이콘 다시 더블클릭 → 새 버전(`bc231bf` + 잠재 fix들).

체크 포인트:
1. **첫 화면** — NoFile 카드 + 상단 닫기/최소화/파일 열기 명확히
2. **다른 앱으로 alt-tab** — Deno Player 컨트롤이 다른 앱 위에 절대 안 뜸
3. **영상 더블클릭 두 번** (탐색기에서 두 파일 연속) — 두 번째는 첫 인스턴스에서 열려야 함 (창 하나만)
4. **재생목록 토글** — `P` 키 / 우상단 📋 아이콘 (그린이면 켜진 상태)
5. **재생 중인 항목 자동 스크롤** — 큰 폴더에서 현재 곡이 보임
6. **풀스크린 `F`** — 영상 전체 + 컨트롤 자동 숨김

## 잠재 버그/제약 (사용자 손 테스트 이후 추가 fix 여지)

| 항목 | 내용 |
|---|---|
| WindowChrome + 풀스크린 토글 | 한 프레임 깜빡임 가능 |
| 멀티 모니터 HiDPI | `PerMonitorV2` manifest 설정 — 모니터 이동 시 확인 |
| Glyph 누락 | Segoe Fluent Icons / MDL2 Assets 없으면 □ 표시 |
| Volume slider 변경 빈도 | mpv 명령 폭주 — 현재 영향 없으나 throttle 여지 |
| 이미지 모드 폴백 | mpv가 이미지 표시 실패 시 WPF Image fallback 없음 |
| 영상 위 컨트롤 오버레이 | 현재 없음 (Grid 레이아웃). owned-window 방식이 별도 라운드 |
| 우측 끝 hover 슬라이드 | 제거됨. 명시적 토글로 대체 |
| 첫 화면 시각 검증 | 사용자 손 테스트에서만 확인 가능 |

## 다음 세션 첫 지시문 (사용자 결정 대기)

1. 사용자 손 테스트 → 시각 동작 피드백
2. 피드백 반영 (특히 영상 위 컨트롤 오버레이가 필요하면 owned-window
   라운드 진행)
3. GitHub repo 만들기 + push (사용자 명시 승인 후만)
4. Manager 노출 / Codex 메모리 반영은 사용자 승인 후

— Claude Code (Opus 4.7)
