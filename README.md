# GameRoot

A Windows console tool that scans PC settings and reports each one as **On** or **Off**.
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

Requires the .NET 8 SDK.

```
dotnet publish src/GameRoot/GameRoot.csproj -c Release -r win-x64 -o publish
```

This produces a single self-contained `publish/GameRoot.exe`. Run with `--no-pause` to skip the
"Press any key" prompt. Every push also builds the exe in GitHub Actions (download it from the run's artifacts).

## Adding a check

Implement `ICheck` in `src/GameRoot/Checks/` and add it to a section in `Program.cs`. Each section is
followed by a separator line.
