# DinamicScreenSound

DinamicScreenSound is a Windows 11 project that aims to make each app’s audio follow its window between displays.

The console prototype lists displays and active audio outputs. Use it to identify devices before testing per-app audio routing.

## Build and run

From a Windows 11 desktop session with the .NET 10 SDK installed:

```powershell
dotnet run --project src/DinamicScreenSound
```

The read-only inventory shows the primary display, display coordinates, and the default Windows audio output.

## License

[MIT](LICENSE)
