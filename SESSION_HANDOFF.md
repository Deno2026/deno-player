# Deno Player — Session Handoff

**Status (2026-05-24):** v0.1.0 MVP 코드 작성 완료, `dotnet build -c Release` ✅,
smoke test에서 앱+mpv 동시 기동 확인 ✅. 사용자 손 테스트 대기.

## 어디까지 했나

| 영역 | 상태 |
|---|---|
| 프로젝트 스캐폴드 (.NET 8 WPF, net8.0-windows, x64) | ✅ |
| `Themes/DesignTokens.xaml` (단일 디자인 출처) | ✅ |
| Services: `MpvProcessService`, `MpvIpcClient`, `PlaylistService`, `SettingsService` | ✅ |
| Helpers: `Win32VideoHost` (child Win32 window), 자연 정렬, 변환기, RelayCommand | ✅ |
| `ViewModels/MainViewModel` (state, commands, mpv property 동기화) | ✅ |
| `MainWindow.xaml(.cs)` (WindowChrome 얇은 chrome, 컨트롤 자동 숨김, 우측 hover 재생목록, fullscreen) | ✅ |
| 단축키 (Space/←/→/↑/↓/M/F/F11/PageUp/PageDown/. /, /Ctrl+O/S/T) | ✅ |
| Drag&drop, 명령줄 인자, 폴더 드롭→첫 미디어 | ✅ |
| `tools/fetch-mpv.ps1` (zhongfly/mpv-winbuild 자동 다운로드, 7z 해제) | ✅ 실제 실행 검증 |
| README / LICENSE (MIT) / NOTICE (mpv GPL 분리) | ✅ |
| `.github/workflows/build.yml` (push/PR/tag 빌드, tag 시 zip artifact) | ✅ |
| `dotnet publish -c Release -r win-x64 --self-contained false` | ✅ `publish/DenoPlayer-win-x64/` |
| git init main + 첫 커밋 `d142d78` | ✅ |
| GitHub push | ⛔ 사용자 명시 승인 전 금지 |

## 즉시 사용자가 할 일

```pwsh
cd E:\DENO-Repos\deno-player
.\bin\Release\net8.0-windows\DenoPlayer.exe
# 또는 파일 인자
.\bin\Release\net8.0-windows\DenoPlayer.exe "C:\some\clip.mp4"
```

mpv.exe는 이미 `runtime\mpv\mpv.exe`에 받아둠 (v0.41.0-689).

## 알려진/유의 사항 (needs-verification)

1. **WPF airspace** — mpv가 child Win32 window에 비디오를 그리는 동안, 같은
   윈도우 안의 WPF 컨트롤(하단 transport, 우측 playlist, 상단 bar)이
   비디오 위에 안 보일 가능성. 빌드 시점엔 확인 못 함. 만약 컨트롤이
   비디오에 가려지면 → 컨트롤들을 `Popup`이나 owned `Window`로 옮겨야
   함 (별도 hwnd라 z-order 회피 가능). 첫 손 테스트의 핵심 체크포인트.
2. **WindowChrome + Fullscreen** — F/F11 전환 시 `WindowChrome`을 잠시
   null로 설정 후 복원. 깜빡임/잔상 가능. 한 번 토글해서 확인 필요.
3. **고DPI** — `app.manifest`에 `PerMonitorV2` 명시. 멀티 모니터 DPI
   다른 환경에서 한 번 확인 필요.
4. **mpv image 모드 fallback** — 현재 mpv가 이미지 표시 실패해도 WPF로
   fallback 안 함. 대부분 이미지는 mpv가 처리하므로 MVP 범위에선 OK.
   필요시 WPF `Image` 컨트롤 fallback 추가.
5. **fetch-mpv.ps1** — community release(zhongfly)에 의존. release가
   사라지면 README의 수동 설치 안내로 대체 가능.

## 풀스택 검증 체크리스트 (사용자 손 테스트)

- [ ] 앱 실행 — NoFile 첫 화면 정상 표시
- [ ] 비디오 드래그 → 즉시 재생 + 같은 폴더 자동 playlist
- [ ] Space play/pause, ← → seek, ↑ ↓ 볼륨, M mute
- [ ] 우측 끝 hover → playlist slide-in, 이탈 1s 후 slide-out
- [ ] PageUp/Down 이전/다음 + EOF 시 자동 다음
- [ ] F 전체화면, Esc 해제, 작업표시줄 숨김 확인
- [ ] Ctrl+S → `Pictures\DenoPlayer\<name>_yyyymmdd_HHmmss.png`
- [ ] Ctrl+T → 항상 위, 아이콘 accent 색
- [ ] 마우스 멈춤 2.5초 → 하단/상단 컨트롤 페이드아웃
- [ ] 마우스 movement → 페이드인
- [ ] 이미지(.png .jpg) 열기 — mpv `image-display-duration=inf`로 유지
- [ ] 오디오(.mp3) 열기 — 영상 영역 검은색, 재생 정상
- [ ] 윈도우 크기/위치/볼륨 변경 → 종료 후 재실행 시 복원
- [ ] mpv.exe 부재 시 NoFile 화면 + "mpv.exe 찾을 수 없습니다" 안내

## 폴더 구조

```
deno-player/
├─ DenoPlayer.csproj
├─ app.manifest                       — PerMonitorV2, Win10/11
├─ App.xaml / App.xaml.cs             — 진입점, 인자 처리, unhandled exc
├─ MainWindow.xaml / .xaml.cs         — 메인 셸, 자동숨김, 풀스크린
├─ Themes/DesignTokens.xaml           — ★ 디자인 단일 출처(브러시/간격/타이밍)
├─ Models/
│   ├─ MediaKind.cs                   — 확장자→Kind, 지원 확장자 단일 출처
│   ├─ MediaItem.cs
│   └─ AppSettings.cs                 — %APPDATA%\DenoPlayer\settings.json
├─ Services/
│   ├─ MpvProcessService.cs           — mpv.exe 실행/종료/crash
│   ├─ MpvIpcClient.cs                — Windows named pipe JSON IPC
│   ├─ PlaylistService.cs             — 폴더 스캔 + 자연 정렬
│   └─ SettingsService.cs
├─ ViewModels/
│   ├─ PlayerState.cs                 — NoFile/Dragging/Loading/Ready/Playing/Paused/Failed
│   └─ MainViewModel.cs               — 상태, 명령, mpv property 양방향 매핑
├─ Helpers/
│   ├─ Win32VideoHost.cs              — HwndHost 직접 구현 (mpv --wid 타깃)
│   ├─ Win32.cs                       — Win32 헬퍼
│   ├─ NaturalStringComparer.cs       — "file2 < file10"
│   ├─ RelayCommand.cs
│   ├─ TimeFormat.cs                  — mm:ss / h:mm:ss
│   └─ Converters.cs                  — Bool/State→Visibility, glyph 변환기
├─ runtime/mpv/                       — mpv.exe (gitignore)
├─ tools/fetch-mpv.ps1                — 자동 다운로드
└─ .github/workflows/build.yml
```

## DENO DNA 반영 메모

- 첫 화면: 마케팅 문구/카드/그라디언트 없음. 6×6 accent dot + `DENO PLAYER`
  monospace + "미디어를 끌어다 놓으세요" + 짧은 단축키 한 줄.
- 색상: near-black `#0B0E0C` ~ `#161D14` 계조 + DENO green `#57E389`.
  Green은 재생 중 dot, 활성 토글, drop ready border, seek played 영역,
  현재 항목 좌측 active bar 4곳에만.
- 컨트롤: 텍스트 대신 Segoe Fluent Icons 글리프. transport bar는 한 줄.
- 실패 상태: 메시지 + 다음 행동("다른 파일 열기", "다음 파일") 함께 제공.
- 설정 UI 없음 — settings.json 1개 파일. 디자인 튜닝 값(컨트롤 자동 숨김
  지연 등)은 settings.json으로 수정 가능, 별도 UI는 노출 안 함.
- 광고/계정/클라우드/플러그인/AI/텔레메트리 코드 0줄 (검증: `grep -ri
  "http\|api\|account\|telemetry\|analytics"` 결과 없음 — fetch-mpv.ps1
  내부 GitHub release 호출은 빌드 외 도구라 본 앱과 무관).

## 다음 세션 첫 지시문 (사용자 결정 대기)

1. 사용자 손 테스트 → 시각 동작 피드백 (특히 airspace, fullscreen, hover).
2. 피드백 반영 (Popup 전환 등 필요 시).
3. GitHub repo 만들기 + push (사용자 승인 후).
   - 후보 이름: `deno-player`
   - 첫 태그: `v0.1.0` → workflow가 자동으로 zip artifact 생성
4. Manager 노출 / Codex 메모리 ad_hoc 노트 반영은 사용자 승인 후.

— Claude Code (Opus 4.7)
