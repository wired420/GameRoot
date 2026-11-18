# GameRoot

A Windows app that scans PC settings and reports each one as **On** or **Off**. It has three tabs:

- **Security** (opens on launch): the settings report below.
- **Services**: every running Windows service.
- **Drivers**: every loaded driver.
- **About**: the logo, version and a link to this repository.

Click a column header to sort, and Ctrl+C copies selected rows. **Export report...** in the bottom bar
saves all three tabs to a text file.

GameRoot runs as administrator (it prompts via UAC on launch). After a warning prompt, a scanning window with the
GameRoot logo and a progress bar runs the scan; the main window opens with the results when it finishes. The scan is read-only, with one exception:
the **Windows Settings** (Game Mode, GPU scheduling, Fast Boot) have a slider at the start of their row to
turn them on or off. The Windows Security protections and BIOS items are never changed by the app; instead each Windows Security
heading has one **Open** button (each heading is a single page) that jumps straight to it in Windows;
the setting is changed there, in Windows' own UI. TPM and Secure Boot are firmware settings changed in the BIOS.

## What it checks

1. **TPM** and **Secure Boot**, each a plain On or Off.
2. **Game Mode**, **Hardware-accelerated GPU scheduling** and **Fast Boot**.
3. **Windows Security**, matching the pages of the Windows Security app (minus health, family options and history):
   - Virus & threat protection: Defender Antivirus, real-time, cloud-delivered, sample submission, Tamper Protection, controlled folder access
   - Account protection: Dynamic lock
   - Firewall & network protection: domain, private and public networks
   - App & browser control: Smart App Control, Check apps and files, SmartScreen for Edge and Store apps, phishing protection, PUA blocking
   - Exploit protection (system settings): CFG, DEP, force randomization, bottom-up ASLR, high-entropy ASLR, SEHOP, heap integrity
   - Device security: core isolation (VBS), memory integrity, LSA protection, vulnerable driver blocklist, kernel stack protection

A separator line follows each section. Values are On or Off, except where Windows has a third state
(Evaluation mode, Audit mode), a feature does not exist on the PC ("Not available on this PC"), or a value
can only be read as administrator ("Needs admin to read").

## Build

GameRoot targets **.NET Framework 4.8.1**, which comes with Windows 11, so users don't need to install anything.
Building needs the .NET SDK (any recent version, on Windows or Linux):

```
dotnet build src/GameRoot/GameRoot.csproj -c Release
```

The output is `bin/Release/net481/GameRoot.exe` (about 50 KB) plus `GameRoot.exe.config`, which holds
high-DPI settings; keep the two together. Every push also builds them in GitHub Actions. The app manifest
requests administrator rights, so debugging from Visual Studio needs VS run as administrator.

## Adding a check

Implement `ICheck` in `src/GameRoot/Checks/` and add it to a section in `SecurityReport.cs`. Each section is
followed by a separator line. To let a setting be changed as well as read, also implement `IToggle`; a slider
then appears on its row. This is reserved for non-security settings.
