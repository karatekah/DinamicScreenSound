# DinamicScreenSound

DinamicScreenSound is a Windows 11 project that aims to make each app’s audio follow its window between displays.

The desktop app runs in the Windows notification area. Assign an output to each display, then start automatic routing for apps whose audio sessions and windows can be matched.

## Build and run

From a Windows 11 desktop session with the .NET 10 SDK installed:

```powershell
dotnet run --project src/DinamicScreenSound -- tray
```

In **Settings**, choose an output for each display you want to control and select **Save**. If Windows hides the tray icon, open the notification area overflow. Right-click the icon and select **Start routing**. The app saves the assignments under `%LOCALAPPDATA%\DinamicScreenSound\outputs.json` and starts paused each time it opens.

An app keeps its current output while its windows span different displays. If an assigned device is unavailable, routing uses the Windows default output. **Pause routing** keeps the current outputs. **Exit** restores changes for apps that are still running and preserves any manual output changes made in Windows.

To produce a portable desktop executable for Windows x64:

```powershell
dotnet publish src/DinamicScreenSound -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DesktopBuild=true -o src/DinamicScreenSound/bin/desktop/win-x64
```

## Console diagnostics

```powershell
dotnet run --project src/DinamicScreenSound -- list
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

## Follow a window from the console

To test with Brave, keep a single browser window open and play a video. Find the PID of the window and the PID of its audio session. Brave can use separate processes for each.

```powershell
Get-Process brave | Where-Object { $_.MainWindowHandle -ne [IntPtr]::Zero } | Select-Object Id
dotnet run --project src/DinamicScreenSound -- sessions

$windowPid = 12345 # Replace with the window PID.
$audioPid = 67890 # Replace with the audio session PID.
dotnet run --project src/DinamicScreenSound -- follow $windowPid $audioPid 'Primary display output' 'Secondary display output'
```

Replace the output names with those shown by `list`. Move the window to the other display and back while the video plays. In the console, press `Enter` or `Ctrl+C` to restore the previous output before closing Brave.

In the initial Windows 11 test, Brave kept playing while its audio switched from Realtek to the external display and back in under a second per switch. A second session stayed on Realtek. Exiting with the window on the second display restored Brave’s original output.

## Contributing

Use English for code, documentation, and commit messages. Use the `type(scope): summary` format for commits.

Run the routing checks with `dotnet run --project tests`.

## License

[MIT](LICENSE)
