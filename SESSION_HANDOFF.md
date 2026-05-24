# Deno Player — Session Handoff

**Status (2026-05-24, v0.1.1):** Airspace 버그 fix + 탐색기 통합 install.ps1
적용. 사용자 PC에 바탕화면 바로가기/시작 메뉴/연결 프로그램 모두 등록 완료.
실제 영상 표시는 사용자 손 테스트 대기.

## v0.1.0 → v0.1.1 변경 (이번 라운드)

사용자 1차 피드백:
> "이 프로그램 목적이 탐색기에서 실행하는 게 목적인데, 그리고 그냥
> 시커먼 화면만 나오고 반응이 없어"

원인: WPF **airspace** 문제 — mpv가 그리는 child Win32 window가 같은
윈도우의 WPF 컨트롤(NoFile 카드, 하단 transport, 우측 playlist 등)을
z-order에서 무조건 덮어쓰기 때문. v0.1.0 SESSION_HANDOFF에 known
caveat으로 적었던 게 그대로 터짐.

수정:

| 변경 | 효과 |
|---|---|
| `VideoHostContainer.Visibility`를 `State` 바인딩(`ShowVideoHost`: Playing/Paused/Ready/Loading 일 때만 Visible, 나머지 **Hidden** — Collapsed가 아니라서 hwnd는 살아있음) | NoFile/Failed/Dragging 상태에서 카드/오류 메시지가 정상 표시 |
| TopBar / BottomBar / PlaylistHotZone / PlaylistPanel 을 모두 **`Popup`(IsOpen=True 유지)** 으로 wrap. Popup은 각자 별도 hwnd라 z-order에서 영상 hwnd 위로 떠 airspace 회피 | 영상 재생 중에도 컨트롤/재생목록이 영상 위에 보임 |
| Popup placement `Relative` + 코드비하인드의 `UpdatePopupLayout()`이 `SizeChanged`/`LocationChanged`/`StateChanged` 시점에 모든 Popup `HorizontalOffset`/`VerticalOffset` 재계산 | 윈도우 이동/리사이즈/최대화/풀스크린 시 컨트롤 위치 동기화 |
| `WindowState.Minimized` ↔ 복원 시 Popup `IsOpen` 토글 | 최소화 잔상(Popup이 화면에 떠 있음) 방지 |
| `MpvIpcClient` 에 **`mouse-pos`** observe 추가. ViewModel에서 `MouseActivity` 이벤트로 노출 → MainWindow가 구독해서 영상 hwnd 위 마우스 이동 시에도 OSD 페이드인 + 자동 숨김 타이머 리셋 | 영상 위 마우스 움직임도 컨트롤 표시 트리거 (영상 hwnd가 WPF MouseMove 가로채는 문제 해결) |
| `tools/install.ps1` 신규 — HKCU 등록 + 바로가기 | 사용자가 한 줄로 탐색기 통합 완료 |
| README "받자마자 쓰기" 섹션 — install.ps1 한 줄 안내가 최상단 | 비개발자 진입 경로 단순화 |

## 어디까지 했나

| 영역 | 상태 |
|---|---|
| 프로젝트 스캐폴드 (.NET 8 WPF, net8.0-windows, x64) | ✅ |
| `Themes/DesignTokens.xaml` 단일 디자인 출처 | ✅ |
| Services: Mpv 프로세스/IPC, Playlist, Settings | ✅ |
| Helpers: `Win32VideoHost` (child HWND), 자연 정렬, 변환기, RelayCommand | ✅ |
| ViewModel: state, commands, mpv property 양방향, **MouseActivity 이벤트** | ✅ |
| MainWindow: WindowChrome + **컨트롤 Popup 4종 + 위치 동기화** | ✅ |
| 단축키, drag&drop, 명령줄 인자 | ✅ |
| `tools/fetch-mpv.ps1` (mpv 자동 다운로드, 검증됨 v0.41.0-689) | ✅ |
| `tools/install.ps1` (HKCU 등록 + Desktop/StartMenu 바로가기) | ✅ 실제 등록 완료 |
| README / LICENSE / NOTICE / .gitignore / .gitattributes | ✅ |
| `.github/workflows/build.yml` | ✅ |
| `dotnet publish -c Release -r win-x64 --self-contained false` | ✅ `publish/DenoPlayer-win-x64/` |
| smoke test (3초 기동, 앱+mpv 살아있음, 정상 종료) | ✅ |
| 시각 검증 (NoFile 카드 표시, 컨트롤 페이드인, 영상 위 컨트롤) | ⏳ 사용자 손 테스트 |
| GitHub push | ⛔ 사용자 명시 승인 전 금지 |

## 즉시 사용자가 할 일

**바탕화면에 "Deno Player" 아이콘이 생겼습니다.**

1. **바탕화면 아이콘 더블클릭** → NoFile 카드 보여야 정상
   - 가운데에 작은 그린 dot + `DENO PLAYER` + "미디어를 끌어다 놓으세요"
   - 검은 화면만 나오면 **즉시 알려주세요** (airspace fix가 또 안 통한 것)
2. **테스트할 미디어 파일을 끌어다 놓기**
   - 영상이 나타나야 함
   - 마우스 움직이면 하단 transport bar 페이드인
   - 마우스 멈춤 2.5초 후 페이드아웃
   - 우측 끝 24px 영역에 마우스 가져가면 재생목록 슬라이드 인
3. **탐색기에서 영상 우클릭 → 연결 프로그램 → Deno Player**
   - 즉시 재생 + 같은 폴더 자동 재생목록
4. **단축키 확인**: Space, ← →, ↑ ↓, F, M, PageUp/Down

## 알려진/유의 사항 (needs-verification, 사용자 손 테스트로만 확인)

1. **컨트롤 표시 — airspace fix 실측**
   첫 화면에서 NoFile 카드가 보이는지가 1순위. 그게 보이면 fix 성공.
   영상 재생 후 마우스 움직임 시 하단 컨트롤이 영상 위에 보이는지가 2순위.
2. **풀스크린 토글 시 깜빡임/잔상** — WindowChrome을 잠시 null로 변경 후
   복원하므로 한 프레임 정도 깜빡일 수 있음. 거슬리면 알림.
3. **고DPI** — `PerMonitorV2` manifest 설정. 멀티 모니터 환경에서 옮길 때
   UI 크기/Popup 위치가 흐트러지는지 확인.
4. **mpv image 모드 fallback** — 현재 이미지도 mpv로 표시. 실패 시 WPF
   Image fallback은 아직 없음. 대부분 동작할 것.
5. **Popup 처음 위치** — Window가 화면에 표시되기 전에 IsOpen=true가 되면
   Popup이 좌상단(0,0)에 잠깐 떴다가 자리 잡을 수 있음. `OnRootLoaded`
   에서 IsOpen 전환 후 UpdatePopupLayout 2번 호출로 보정함.

## 폴더 구조

```
deno-player/
├─ DenoPlayer.csproj
├─ app.manifest                       — PerMonitorV2 DPI
├─ App.xaml / App.xaml.cs             — 진입점, 인자 처리
├─ MainWindow.xaml / .xaml.cs         — Popup 기반 셸, 위치 동기화
├─ Themes/DesignTokens.xaml           — ★ 디자인 단일 출처
├─ Models/
│   ├─ MediaKind.cs                   — 확장자→Kind, 지원 확장자 단일 출처
│   ├─ MediaItem.cs
│   └─ AppSettings.cs                 — %APPDATA%\DenoPlayer\settings.json
├─ Services/
│   ├─ MpvProcessService.cs           — mpv.exe 실행/종료
│   ├─ MpvIpcClient.cs                — Named pipe JSON IPC + mouse-pos observe
│   ├─ PlaylistService.cs             — 폴더 스캔 + 자연 정렬
│   └─ SettingsService.cs
├─ ViewModels/
│   ├─ PlayerState.cs                 — NoFile/Dragging/Loading/Ready/Playing/Paused/Failed
│   └─ MainViewModel.cs               — 상태/명령/IPC mapping + MouseActivity
├─ Helpers/
│   ├─ Win32VideoHost.cs              — HwndHost 직접 구현 (mpv --wid 타깃)
│   ├─ Win32.cs, NaturalStringComparer.cs, RelayCommand.cs, TimeFormat.cs
│   └─ Converters.cs                  — Bool/State→Visibility, glyph 변환기
├─ runtime/mpv/mpv.exe                — 사용자가 fetch-mpv.ps1로 받음 (gitignore)
├─ tools/
│   ├─ fetch-mpv.ps1                  — mpv 자동 다운로드
│   └─ install.ps1                    — HKCU 등록 + Desktop/StartMenu 바로가기
├─ publish/DenoPlayer-win-x64/        — framework-dependent 배포본
└─ .github/workflows/build.yml
```

## DENO DNA 반영 메모 (변동 없음, 재확인)

- 첫 화면: 마케팅 카피/카드/그라디언트 없음. 6×6 accent dot + DENO PLAYER mono
  + "미디어를 끌어다 놓으세요" + 짧은 단축키 한 줄.
- 색: near-black `#0B0E0C` ~ `#161D14` 계조 + DENO green `#57E389`.
  Green은 재생 dot / 활성 토글 / drop ready / seek played / active item bar에만.
- 컨트롤: Segoe Fluent 글리프 중심, transport bar 한 줄, 마우스 멈춤 페이드아웃.
- 실패 상태: 메시지 + 다음 행동(다른 파일 열기 / 다음 파일) 함께 제공.
- 광고/계정/네트워크/플러그인/AI/텔레메트리 0줄.

## 다음 세션 첫 지시문

1. 사용자 손 테스트 → 시각 동작 피드백 (특히 첫 화면 NoFile 카드, 영상
   재생 중 컨트롤, 우측 hover playlist).
2. 피드백 반영.
3. GitHub repo 만들기 + push (사용자 명시 승인 후만).
4. Manager 노출 / Codex 메모리 반영은 사용자 승인 후.

— Claude Code (Opus 4.7)
