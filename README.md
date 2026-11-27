# DinamicScreenSound

DinamicScreenSound is a Windows 11 project that aims to make each app’s audio follow its window between displays.

The desktop app runs in the Windows notification area. Assign an output to each display, then start automatic routing for apps whose audio sessions and windows can be matched.

## Install and run on Windows

From the repository root with the .NET 10 SDK installed, run:

```powershell
.\scripts\Install-Desktop.ps1
```

The script runs the routing checks, builds the Windows x64 executable, and updates the **DinamicScreenSound** desktop shortcut to the installed build. Use that shortcut for everyday use. If an older instance is running, select **Exit** in its notification area menu before opening the shortcut. This restores its audio output changes.

## Test a development build

From a Windows 11 desktop session, run a development build from the checkout:

```powershell
dotnet run --project src/DinamicScreenSound -- tray
```

In **Settings**, choose an output for each display you want to control and select **Save**. If Windows hides the tray icon, open the notification area overflow. Right-click the icon and select **Start routing**. The app saves the assignments under `%LOCALAPPDATA%\DinamicScreenSound\outputs.json` and starts paused each time it opens.

In **Settings**, select **Identify displays** or press **Alt+I** to show a large number on each screen for four seconds. Match that number to the display's output selector. Select **Identify displays** again after connecting or repositioning a monitor to refresh the rows and keep your pending choices.

Look for the blue monitor icon. A waveform indicates active routing, and pause bars indicate paused routing. The tooltip gives the status in text.

An app keeps its current output while its windows span different displays. The tray shows **Shared app audio spans displays** when that prevents a switch. Move the app's windows onto the same display to resume automatic routing. If an assigned device is unavailable, routing uses the Windows default output. **Pause routing** keeps the current outputs. **Exit** restores changes for apps that are still running and preserves any manual output changes made in Windows.

## Follow a specific window

Open **Follow this window** from the notification area, select a window, and choose **Follow window**. While routing is active, all audio from that app follows the selected window even if its other windows are on different displays. The audio stays on its current output while the selected window is minimized or being moved.

The choice expires when the window closes or DinamicScreenSound exits. To clear it sooner, select a window from the same app and choose **Use automatic routing**. Fixed outputs in **App Exceptions** take priority.

## Set a fixed output for an app

Open **App Exceptions** from the notification area icon. Pick an application from the list or use **Browse** to select its `.exe`. Choose a fixed audio output and select **Save**. While routing is active, the fixed output takes priority over display routing, including for apps playing in the background. If the fixed device disconnects, the app uses the Windows default output and returns to the fixed device when it reconnects.

An exception applies to the whole application. For example, a rule for `brave.exe` applies to its calls, tabs, and windows. Choose **Follow display** to remove a rule. The next eligible display determines the output. Rules are saved in `%LOCALAPPDATA%\DinamicScreenSound\exceptions.json`.

Use **Tab** to reach the application and output selectors, the **Arrow** keys to change a selection, **Alt+S** to save, and **Esc** to cancel. Use the Windows notification area to reach the tray menu with a keyboard or screen reader.

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
