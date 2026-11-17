# GameRoot

A Windows app that scans PC settings and reports each one as **On** or **Off**. It has three tabs:

- **Security** (opens on launch): the settings report below.
- **Services**: every running Windows service.
- **Drivers**: every loaded driver.

Click a column header to sort, and Ctrl+C copies selected rows.
Version 1 is read-only: it changes nothing.

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
high-DPI settings; keep the two together. Every push also builds them in GitHub Actions.

## Adding a check

Implement `ICheck` in `src/GameRoot/Checks/` and add it to a section in `SecurityReport.cs`. Each section is
followed by a separator line.
