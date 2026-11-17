# GameRoot

A Windows console tool that scans PC settings and reports each one as **On** or **Off**.
Version 1 is read-only: it changes nothing.

## Output

```
GameRoot - system check (read-only, nothing is changed)
--------------------------------------------------
TPM                                  : On
Secure Boot                          : On
--------------------------------------------------
Game Mode                            : On
Hardware-accelerated GPU scheduling  : Off
Fast Boot                            : On
--------------------------------------------------
```

| Check | How it is read |
|---|---|
| TPM | `Tbsi_GetDeviceInfo` in `tbs.dll` (no admin needed). On when Windows can see a TPM. |
| Secure Boot | `HKLM\SYSTEM\CurrentControlSet\Control\SecureBoot\State\UEFISecureBootEnabled` = 1. Off on legacy BIOS. |
| Game Mode | `HKCU\Software\Microsoft\GameBar\AutoGameModeEnabled` (missing = On, Windows default). |
| Hardware-accelerated GPU scheduling | `HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\HwSchMode` = 2. |
| Fast Boot | `HKLM\...\Session Manager\Power\HiberbootEnabled` = 1 and hibernation enabled. |

## Build

Requires the .NET 8 SDK.

```
dotnet publish src/GameRoot/GameRoot.csproj -c Release -r win-x64 -o publish
```

This produces a single self-contained `publish/GameRoot.exe`. Run with `--no-pause` to skip the
"Press any key" prompt. Every push also builds the exe in GitHub Actions (download it from the run's artifacts).

## Adding a check

Implement `ICheck` in `src/GameRoot/Checks/` and add it to a section in `Program.cs`. Each section is
followed by a separator line.
