# DinamicScreenSound

DinamicScreenSound is a Windows 11 project that aims to make each app’s audio follow its window between displays.

The console prototype lists displays, audio outputs, and active audio sessions. It includes a reversible test for measuring how long a playing app takes to switch outputs.

## Build and run

From a Windows 11 desktop session with the .NET 10 SDK installed:

```powershell
dotnet run --project src/DinamicScreenSound
```

The read-only inventory shows the primary display, display coordinates, and the default Windows audio output.

## Test an output switch

Play audio in an app, find its audio session PID with `sessions`, and choose the exact name of another output from `list`.

```powershell
dotnet run --project src/DinamicScreenSound -- sessions
$audioPid = 12345 # Replace with the PID of the active audio session.
dotnet run --project src/DinamicScreenSound -- probe $audioPid 'Exact output name'
```

Keep the app open during the test. The command attempts to move its active session, measures the time until it detects audio on the target output, and restores the original preference before measuring the switch back. Press `Ctrl+C` to cancel.

Per-app routing uses an internal Windows interface. The measurement depends on the app opening its stream on the new output and continuing to play audio. [Core Audio](https://learn.microsoft.com/en-us/windows/win32/coreaudio/core-audio-interfaces) · [Application audio policy reference](https://github.com/File-New-Project/EarTrumpet/blob/master/EarTrumpet/Interop/MMDeviceAPI/IAudioPolicyConfigFactoryVariantFor21H2.cs)

## License

[MIT](LICENSE)
