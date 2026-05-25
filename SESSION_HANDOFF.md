# Deno Player — Session Handoff

**Status (2026-05-26):** repo PRIVATE 전환 + 초보 친화적 설치 절차로 재정비.
public release/tag 모두 삭제. 65/65 unit tests pass, build 경고 0 / 오류 0.

## Repository

- **Repo**: https://github.com/Deno2026/deno-player (**PRIVATE**)
- **Releases**: 모두 삭제 (이전 v0.3.0~v0.3.3 public release는 사용자 요청으로 cleanup)
- **다음 release**: 친화적 설치 완성 + 사용자 만족 확인 후 결정

## 설치 절차 (재정비)

이전 (PowerShell 2단계 직접 실행) → 현재 (배치 1단계):

1. zip 압축 풀기
2. **`START_HERE.bat`** 더블클릭
3. (한 번만) mpv 자동 다운로드 → 바탕화면 바로가기 + 탐색기 우클릭 메뉴 등록 — 완료

제거: **`UNINSTALL.bat`** 더블클릭.

## 추가된 / 변경된 파일

| 파일 | 내용 |
|---|---|
| [START_HERE.bat](START_HERE.bat) | 더블클릭 → fetch-mpv (-SkipIfExists) + install.ps1 자동 |
| [UNINSTALL.bat](UNINSTALL.bat) | 더블클릭 → install.ps1 -Uninstall 자동 |
| [tools/7zr.exe](tools/7zr.exe) | 7-Zip standalone (602KB) — 사용자가 7-Zip 미설치라도 mpv .7z extract 가능 |
| [tools/fetch-mpv.ps1](tools/fetch-mpv.ps1) | `-SkipIfExists` switch 추가 (이미 mpv 있으면 다운로드 skip) + 7zr.exe 우선 사용 |
| [DenoPlayer.csproj](DenoPlayer.csproj) | `<None Include="tools\**;START_HERE.bat;UNINSTALL.bat;...">` publish 포함 |
| [README.md](README.md) | "설치 3단계" 큰 글자 첫 화면, 나머지 advanced 정보 아래로 |
| [NOTICE.md](NOTICE.md) | 7zr.exe (LGPL) 라이선스 표시 |

## v0.3.0 → v0.3.3 (이전 public release, 이제 cleanup됨)

| 라운드 | 주요 변경 |
|---|---|
| v0.3.0 | 첫 공개 (autonomous 자율 진행 종료 상태) |
| v0.3.1 | 영상 위 click 시 KeyBinding 죽던 버그 (WM_MOUSEACTIVATE → MA_NOACTIVATE), Slider OneWay binding 보존 (SetCurrentValue), PlaylistService forward-slash 경로 중복 시드 |
| v0.3.2 | Toast가 영상 위 안 보이던 airspace 문제 — owned ToastWindow 분리 |
| v0.3.3 | HiDPI/multi-monitor 우측 hover panel 회귀 — mpv mouse-pos 폐기 → GetCursorPos 80ms polling |

## 현재 main HEAD (unreleased)

- a63d93a SESSION_HANDOFF update (v0.3.3 이전)
- a12c490 cleanup: mpv mouse-pos observe/event/throttle 전부 제거
- d9cc9dc Bump 0.3.3
- 69ab2bb GetCursorPos polling
- d79b7df ToastWindow lazy + DPI fix
- (이번 라운드 commit 추가 예정)

## 알려진 미해결 (다음 사이클)

- **PlaylistWindow visual render**: GetCursorPos polling으로 `ShowSlide()` 정상 호출 + position
  정상(Left=1600, W=320, PlaylistCount=23 trace 확인), 그러나 screenshot에 panel contents
  보이지 않음. owned AllowsTransparency child window의 WPF rendering 회귀 의심. 사용자가
  실제로 어떻게 보이는지 확인 필요.

## CI

- main / PR push마다 `build` job (test 포함)
- `v*` tag push 시 `release` job — 현재 tag 없음, 사용자 결정 후 진행

— Claude Code (Opus 4.7)
