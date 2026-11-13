# Microsoft Store 제출 가이드 (MonitorBridge)

목표: Store 배포(Microsoft이 서명) → 스마트 앱 컨트롤 설정 없이 설치. GitHub 릴리스는 서명 없이 유지.

## 현재 상태
- `packaging/msix/` 에 MSIX 템플릿(`AppxManifest.xml`)과 빌드 스크립트(`build.ps1`)가 있습니다.
- `-Dev` 로 만든 시험용 MSIX는 자체 서명 인증서라 **로컬 시험 전용**입니다. Store에는 서명 없는 MSIX를 올리면 Microsoft가 서명합니다.
- MSIX로 실행되면 "Windows 시작 시 트레이로 실행" 체크박스는 숨겨집니다(패키지 앱은 HKCU\Run 쓰기가 가상화됨). 시작 프로그램 지원은 Store 통과 후 StartupTask로 추가 예정.

## 사용자가 할 일
1. **Partner Center 개인 계정 등록** (무료): https://storedeveloper.microsoft.com → 개인 선택, 신분증+셀피 신원 확인.
2. **앱 이름 예약**: 새 제품 → MSIX 또는 PWA 앱 → 이름 `MonitorBridge` (이미 쓰이면 `MonitorBridge – Cursor Fix` 등).
3. 예약 후 **Product identity** 3개 값을 알려 주세요: `Package/Identity/Name`, `Package/Identity/Publisher`(CN=...), `PublisherDisplayName`. 그러면 아래처럼 Store용 패키지를 빌드합니다.
   `.\packaging\msix\build.ps1 -Version 0.3.0.0 -IdentityName <Name> -Publisher <Publisher> -PublisherDisplay <표시이름> -ToolsDir <makeappx 폴더>` (-Dev 없이)
4. **제출 양식 작성**: 아래 "제출 문안"을 붙여 넣고, 스크린샷 1장 이상(1366×768 이상) 올립니다. 연령 등급 설문, 가격(무료) 선택.
5. 제출 후 **인증(심사) 결과**를 확인합니다. `runFullTrust`와 입력 훅에 대한 질문이 오면 아래 설명을 사용합니다.
6. 승인되면 **SAC가 켜진 PC에서 Store 설치본이 실행되는지** 확인해 주세요(핵심 검증).

## 제출 문안 (영어)
**Description:** MonitorBridge fixes cursor jumps and stuck edges on multi-monitor setups with different resolutions and display scaling. Describe your real desk layout (monitor sizes and positions in millimeters) and the cursor crosses between monitors at the physically matching point. It does not change Windows display settings, needs no administrator rights, and restores default Windows behavior immediately when turned off or closed. Everything stays on your PC; no data is sent anywhere.

**Privacy policy URL:** https://github.com/Lee-D-Sen/MonitorBridge/blob/main/CODE_SIGNING_POLICY.md#privacy

**Restricted capability (runFullTrust) justification:** The app is a Win32/WPF desktop utility. It uses a low-level mouse hook (SetWindowsHookEx WH_MOUSE_LL) and Raw Input only to reposition the cursor when it crosses a monitor boundary, and it reads monitor geometry/EDID size from Windows. It does not record, store or transmit input. The hook is removed immediately when correction is turned off or the app exits. These APIs are not available to sandboxed UWP apps.

**Search terms:** multi monitor, mouse cursor, mixed DPI, monitor layout, cursor jump

## 위험 요소
- 심사에서 전역 마우스 훅을 이유로 거절될 수 있습니다. 거절 사유를 받으면 문안을 보완해 재제출합니다.
- Store 설치본이 SAC를 통과하는지는 아직 검증 전입니다.