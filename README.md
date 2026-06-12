# NMEA Receiver

UDP 포트로 NMEA 문장을 수신하고, 기존 Sentence 처리 로직을 거쳐 IOSSentenceSocket UDP 출력으로 전달하는 WPF 애플리케이션입니다.

## 주요 기능

- **다중 UDP 수신 채널 관리**: 여러 UDP bind port를 동시에 열어 NMEA 문장을 수신합니다.
- **기존 Sentence 처리 유지**: 수신된 바이트 데이터는 기존 `NmeaSentenceProcessorService` 로직으로 전달됩니다.
- **IOSSentenceSocket UDP 출력 유지**: 처리된 Sentence 정보는 기존 UDP destination 설정으로 송신됩니다.
- **채널별 UDP Destination 관리**: 각 수신 채널마다 출력 대상 UDP 주소/포트를 관리할 수 있습니다.
- **INI 저장/복원**: UDP bind port, 출력 UDP destination, 실행 상태를 저장하고 다음 실행 시 복원합니다.

## 기술 스택

| 항목 | 내용 |
|------|------|
| Framework | .NET 8.0 WPF |
| Pattern | MVVM |
| MVVM Toolkit | CommunityToolkit.Mvvm |
| DI | Microsoft.Extensions.DependencyInjection |
| 설정 저장 | INI 파일 |

## 빌드 및 실행

```powershell
dotnet build
dotnet run
```

## UDP 테스트 송신기

수신 테스트용 콘솔 프로그램은 `Tools/UdpNmeaTestSender`에 있습니다.

```powershell
dotnet run --project .\Tools\UdpNmeaTestSender\UdpNmeaTestSender.csproj -- --port 40014
```

반복 송신이 필요하면 `--loop`를 사용합니다.

```powershell
dotnet run --project .\Tools\UdpNmeaTestSender\UdpNmeaTestSender.csproj -- --host 127.0.0.1 --port 40014 --loop --interval 1000
```

기본 송신 Sentence는 현재 구현된 `HTD`, `RSA`, `ROR`, `PYDKN`, `ALF`, `ALC`, `ARC`, `ACN`, `HBT`와 코드상 분기되어 있는 `GGA`입니다. 현재 수신 파서는 checksum 문자열을 제거하지 않고 필드를 파싱하므로, 테스트 송신기는 기본적으로 checksum 없이 전송합니다. 필요 시 `--checksum` 옵션으로 checksum을 붙일 수 있습니다.

## 동작 흐름

1. 상단의 `UDP Bind Port`에 수신 대기할 UDP 포트를 입력합니다.
2. `UDP Destination`에는 처리 결과를 송신할 대상 주소와 포트를 입력합니다.
3. `START`를 누르면 입력한 UDP 포트가 bind되고 수신 대기를 시작합니다.
4. UDP로 들어온 NMEA 문장은 기존 Sentence 처리 로직으로 전달됩니다.
5. 처리된 데이터는 IOSSentenceSocket 출력 로직을 통해 UDP destination으로 송신됩니다.

## 프로젝트 구조

```text
Behaviors/      WPF attached behaviors
Converters/     WPF value converters
Interop/        Native structure marshaling
Models/         NMEA sentence and UI models
Services/       UDP receive, sentence processing, UDP output, INI persistence
Styles/         WPF theme and control styles
ViewModels/     MVVM view models
Views/          WPF views
```
