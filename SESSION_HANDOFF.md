# Deno Player — Session Handoff

**Status (2026-05-26, v0.3.3 + cleanup):** GitHub 공개 배포 완료, v0.3.3까지 release.
65/65 unit tests pass, dotnet build 경고 0 / 오류 0, 작업 트리 clean.

## Repository

- **Repo**: https://github.com/Deno2026/deno-player (public, MIT)
- **Latest release**: https://github.com/Deno2026/deno-player/releases/tag/v0.3.3
- **Downloads**:
  - self-contained (.NET 포함, 받자마자): `DenoPlayer-v0.3.3-win-x64.zip` (~70 MB)
  - framework-dependent (.NET 8 Desktop Runtime 필요): `DenoPlayer-v0.3.3-win-x64-fxdep.zip` (~187 KB)

## v0.3.0 → v0.3.3 변경 요약

| 버전 | 핵심 변경 |
|---|---|
| **v0.3.0** | 첫 공개 release. autonomous 자율 진행 종료 상태. |
| **v0.3.1** | 영상 위 click 시 KeyBinding 죽던 가장 큰 버그 fix (Win32VideoHost WM_MOUSEACTIVATE → MA_NOACTIVATE). Slider OneWay binding 보존 (SetCurrentValue). PlaylistService forward-slash 경로 중복 시드 fix. |
| **v0.3.2** | Toast가 영상 위 안 보이던 airspace 문제 fix — owned ToastWindow로 분리 (PlaylistWindow/RecentWindow 패턴). |
| **v0.3.3** | 우측/좌측 hover panel HiDPI/multi-monitor 회귀 fix — mpv IPC mouse-pos 좌표계 신뢰 안 됨 → Win32 GetCursorPos 80ms polling으로 대체. ShowControls / hot zone trigger 둘 다 polling tick에서 처리. ToastWindow lazy create. |

## 현재 main (unreleased)

| 해시 | 변경 |
|---|---|
| [a12c490](https://github.com/Deno2026/deno-player/commit/a12c490) | cleanup: mpv mouse-pos observe/event/throttle 전부 제거 (polling으로 대체) |

main만 push, tag 없음. 다음 release 시 v0.3.4.

## 시각 검증 (Chrome Remote Desktop, computer-use)

| 항목 | 결과 |
|---|---|
| 영상 launch + IPC connect | ✅ |
| M (mute) | ✅ icon 토글 시각 확인 |
| F / ESC (풀스크린) | ✅ 진입/해제 |
| V (자막 cycle) | ✅ toast "자막 트랙 ▶" 영상 위 표시 (v0.3.2 fix 효과 검증) |
| Space | ✅ pause/resume |
| PageDown | ✅ 다음 파일 |
| Down (volume) | ✅ slider 감소 |
| Repeat 버튼 click | ✅ ↻ ↻ ① glyph cycle |
| Shuffle 버튼 click | ✅ green active |
| 우측/좌측 hover panel | ❓ v0.3.2까지 regression 있었음. v0.3.3 GetCursorPos polling fix는 코드 로직 검증만 (시각 검증은 사용자 자리 부재로 미완). |
| 우클릭 컨텍스트 메뉴 | 코드 OK, 시각 미검증 |
| AlwaysOnTop (Ctrl+T), Settings dialog | 코드 OK, 시각 미검증 |
| 오디오/이미지 파일 | 코드 OK, 시각 미검증 |

## 알려진 trade-off / 의도된 결정

1. **owned child window 패턴 (Playlist/Recent/Toast)** — WPF Popup이 다른 앱 위로 떠다니는 문제 + WPF airspace로 HwndHost 위 시각 못 그림. owned Window는 owner에 묶여 z-order/최소화/닫힘 동기.
2. **GetCursorPos 80ms polling** — mpv mouse-pos가 환경/영상마다 좌표계 (host hwnd native pixel / DIP / video pixel) 다름. 폴링이 DPI-aware + 모든 환경에서 일관 작동.
3. **마지막 재생 위치 저장 안 함** — 의도. 세션 내 자동 진행만.
4. **단축키 설정 UI 없음** — 기본 관습 키 고정.

## CI

- main / PR push마다 `build` job (test 포함)
- `v*` tag push 시 추가 `release` job: framework-dependent + self-contained zip 둘 다 + GitHub Release 자동 생성
- workflow: [.github/workflows/build.yml](.github/workflows/build.yml)

## 다음 사이클 (사용자 결정 대기)

1. **v0.3.3 우측 hover panel 실측 검증** — 사용자가 v0.3.3 zip 다운로드해서 직접 확인. 만약 작동하면 cleanup commit a12c490을 v0.3.4 tag로 release. 작동 안 하면 추가 진단 (DPI 환경 정보 / GetCursorPos 좌표계).
2. **시각 미검증 항목** (우클릭 메뉴, AlwaysOnTop, Settings dialog, 오디오/이미지) — 사용자가 직접 체크해서 거슬리는 부분만 알려주면 같은 case-matrix 루틴으로 즉시 fix.
3. **사용자 요청 추가 기능**.

— Claude Code (Opus 4.7)
