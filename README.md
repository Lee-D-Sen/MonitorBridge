# MonitorBridge

Windows 10/11 멀티 모니터에서 해상도·배율이 다른 화면 사이를 지날 때 마우스 커서가 튀거나 막히는 문제를, **책상 위 실제 물리 배치** 기준으로 보정하는 WPF 앱입니다. Windows 디스플레이 설정은 건드리지 않습니다.

## 동작 방식
- 모니터마다 실제 화면 크기(mm)와 책상 위 위치(mm)를 갖고, 경계를 넘을 때만 커서를 "실제로 같은 높이"의 반대편 지점으로 옮깁니다.
- 책상 위에서 맞닿은 구간(GUI의 초록선)만 통과합니다. 그 밖의 경계는 막히며, 경계를 따라 미끄러지는 동작은 유지됩니다.
- Windows 기본 배치에서 막힌 구간도 GUI에서 연결되어 있으면 넘어갑니다.
- 보정 끄기 / 트레이 종료 시 마우스 훅이 즉시 해제되어 Windows 기본 동작으로 돌아갑니다.

## 첫 설정
0. 처음 실행하면 언어(English / 한국어)를 고르는 창이 뜹니다. 이후 메인 창 아래쪽에서 바꿀 수 있습니다. (On first launch you pick English or Korean; it can be changed later in the main window.)
1. `MonitorBridge.exe` 실행(관리자 권한 불필요).
2. **화면 식별**으로 실제 모니터와 번호를 대조합니다.
3. 오른쪽 패널에서 각 모니터의 **실제 크기**(인치 또는 가로·세로 mm)를 확인합니다. 모니터 정보(EDID)를 못 읽은 경우 24인치 추정값이므로 직접 입력하세요.
4. 모니터를 **드래그**해 책상 위 배치와 높이 차이를 맞춥니다(가장자리에 자동으로 붙고, Alt로 해제). 정밀 조정은 X/Y(mm) 숫자 입력 또는 "이웃 모니터에 맞춤" 버튼.
5. **적용**을 누르면 저장 + 보정이 켜집니다. 창을 닫으면 트레이에서 계속 동작합니다.

설정 파일: `%APPDATA%\MonitorBridge\settings.json`

## 안전장치
- 모니터 연결 해제·해상도 변경·절전 복귀 시 다시 감지합니다. 저장된 모니터와 현재 모니터를 안전하게 짝지을 수 없으면 보정을 끄고 GUI에 알립니다.
- 커서 가두기(ClipCursor)를 쓰는 게임/앱이 있으면 개입하지 않습니다.
- 다른 프로그램이 주입한 커서 이동(SendInput/원격 제어 등)은 기본적으로 무시합니다.

## 빌드 / 테스트
.NET 8 SDK 필요.

```powershell
dotnet test tests/MonitorBridge.Tests -c Release
dotnet publish src/MonitorBridge.App -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o dist/MonitorBridge
```

## 구조
- `src/MonitorBridge.Core` — GUI/Win32와 무관한 좌표 계산(`CursorResolver`, `DeskLayout`), EDID 파서, 모니터 매칭, 설정 모델.
- `src/MonitorBridge.App` — WPF GUI, 트레이, 저수준 마우스 훅 + Raw Input, 모니터 감지. Per-Monitor DPI V2. GUI 좌표(DIP·mm)와 Win32 물리 픽셀을 분리해서 다룹니다.
- `tests/MonitorBridge.Tests` — 4K 150% + FHD 100%, 높이 어긋남, 음수 좌표, 세로 회전, 3대, 비겹침 구간 등.

## 알려진 사항
- 서명되지 않은 빌드는 **Windows 스마트 앱 컨트롤(Smart App Control)** 이 켜진 PC에서 실행이 차단됩니다. 차단되면 코드 서명을 하거나 해당 기능이 꺼진 환경에서 실행해야 합니다.
