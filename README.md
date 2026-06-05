# NMEA Receiver

시리얼 포트(COM)에서 NMEA 문장을 수신하여 UDP로 전송하는 Windows 데스크톱 애플리케이션입니다.

## 주요 기능

- **다중 채널 관리** — 복수의 COM 포트를 동시에 수신, 각 채널별 독립 UDP 전송
- **실시간 모니터링** — 수신된 NMEA 문장 및 파싱 결과 실시간 표시
- **채널 설정 유지** — 앱 종료 후 재시작 시 이전 채널 상태(Running / Stopped) 복원
- **UDP 설정 즉시 반영** — 채널 목록에서 UDP 주소/포트 수정 후 Enter 시 자동 재연결
- **진단 패널** — 원시 NMEA 문장 로그 및 파싱 스냅샷 확인

## 스택

| 항목 | 내용 |
|------|------|
| Framework | .NET 8.0 (WPF) |
| 패턴 | MVVM (`CommunityToolkit.Mvvm`) |
| DI | `Microsoft.Extensions.DependencyInjection` |
| 시리얼 | `System.IO.Ports` |
| 설정 저장 | INI 파일 |

## 빌드 및 실행

```
dotnet build
dotnet run
```

> Windows 전용입니다 (`net8.0-windows`).

## 프로젝트 구조

```
├── Behaviors/          WPF attached behaviors
├── Converters/         IValueConverter 구현체
├── Interop/            네이티브 interop
├── Models/             NMEA 문장 모델
├── Services/           시리얼 수신, UDP 전송, COM 포트, INI 설정
│   └── Interfaces/
├── Styles/             WPF 테마 및 컨트롤 스타일
├── ViewModels/
│   ├── Panels/         각 패널 ViewModel
│   └── Shell/          MainStateStore, MainViewModel
└── Views/
    └── Panels/         채널 설정, 채널 목록, 진단, 스냅샷
```

## 설정 파일

실행 파일과 같은 경로에 `settings.ini`가 자동 생성됩니다. 채널 목록(포트명, 보드레이트, UDP 주소/포트, 실행 상태)이 저장됩니다.
