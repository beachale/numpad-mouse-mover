# Numpad Mouse Mover (Windows)

Move mouse perfectly along x and y axes with numpad for precise control.

## Features

- Perfect axis-only movement:
  - `Numpad 8` / `2` = up / down
  - `Numpad 4` / `6` = left / right
- Runtime speed control:
  - `Numpad +` = increase speed
  - `Numpad -` = decrease speed
- `Numpad 5` = pause/resume movement
- `Numpad .` = toggle numpad key passthrough
- `F12` = exit

## Requirements

- Windows 11 (or modern Windows)
- .NET SDK

## Build

```powershell
dotnet build -c Release
```

## Run

```powershell
dotnet run --project .\NumpadMouseLock
```

## Publish Standalone EXE

```powershell
dotnet publish .\NumpadMouseLock -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

Output:
`NumpadMouseLock\bin\Release\net10.0\win-x64\publish\NumpadMouseLock.exe`

## Notes

- Uses WinAPI `SendInput` with relative mouse movement.
- Some anti-cheat systems may ignore or block simulated input.
- If a game runs as Administrator, run this utility as Administrator too.

## License

MIT
