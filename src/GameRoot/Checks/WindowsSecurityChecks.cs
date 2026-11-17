using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace GameRoot.Checks;

/// <summary>A check defined inline by a name and a function. Used for the many small Windows Security reads.</summary>
public sealed class SimpleCheck(string name, Func<CheckResult> run) : ICheck
{
    public string Name => name;
    public CheckResult Run() => run();
}

/// <summary>
/// Read-only checks that mirror the pages of the Windows Security app.
/// Nothing here changes a setting.
/// </summary>
public static class WindowsSecurityChecks
{
    private const string DefenderScope = @"root\Microsoft\Windows\Defender";
    private const string DeviceGuardScope = @"root\Microsoft\Windows\DeviceGuard";

    private static int Build => Environment.OSVersion.Version.Build;

    private static int? Dword(RegistryHive hive, string key, string value) => RegistryHelper.ReadDword(hive, key, value);

    // ---------------- Firewall & network protection ----------------

    public static ICheck[] Firewall() =>
    [
        FirewallProfile("Domain network", "DomainProfile", "DomainProfile"),
        FirewallProfile("Private network", "StandardProfile", "PrivateProfile"),
        FirewallProfile("Public network", "PublicProfile", "PublicProfile"),
    ];

    private static ICheck FirewallProfile(string name, string localKey, string policyKey) => new SimpleCheck(name, () =>
    {
        // Group Policy wins over the local setting when it is set.
        int? policy = Dword(RegistryHive.LocalMachine, $@"SOFTWARE\Policies\Microsoft\WindowsFirewall\{policyKey}", "EnableFirewall");
        int? local = Dword(RegistryHive.LocalMachine,
            $@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\{localKey}", "EnableFirewall");
        return CheckResult.OnOff((policy ?? local ?? 1) == 1);
    });

    // ---------------- Virus & threat protection (Microsoft Defender) ----------------

    private static object? MpStatus(string p) => WmiHelper.Get(DefenderScope, "MSFT_MpComputerStatus", p);
    private static object? MpPref(string p) => WmiHelper.Get(DefenderScope, "MSFT_MpPreference", p);

    public static ICheck[] Defender() =>
    [
        new SimpleCheck("Microsoft Defender Antivirus", () => CheckResult.OnOff(MpStatus("AntivirusEnabled") is true)),
        new SimpleCheck("Real-time protection", () => CheckResult.OnOff(MpStatus("RealTimeProtectionEnabled") is true)),
        new SimpleCheck("Cloud-delivered protection", () => CheckResult.OnOff(Convert.ToInt32(MpPref("MAPSReporting") ?? 0) != 0)),
        // 1 = send safe samples, 3 = send all. 0 (always prompt) and 2 (never) show as off in Windows Security.
        new SimpleCheck("Automatic sample submission", () =>
            CheckResult.OnOff(Convert.ToInt32(MpPref("SubmitSamplesConsent") ?? 0) is 1 or 3)),
        new SimpleCheck("Tamper Protection", () => CheckResult.OnOff(MpStatus("IsTamperProtected") is true)),
        new SimpleCheck("Controlled folder access", () => Convert.ToInt32(MpPref("EnableControlledFolderAccess") ?? 0) switch
        {
            1 => new CheckResult("On"),
            2 => new CheckResult("Audit mode"),
            _ => new CheckResult("Off"),
        }),
    ];

    // ---------------- App & browser control ----------------

    public static ICheck[] AppAndBrowser() =>
    [
        new SimpleCheck("Smart App Control", () =>
            Dword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\CI\Policy", "VerifiedAndReputablePolicyState") switch
            {
                1 => new CheckResult("On"),
                2 => new CheckResult("Evaluation mode"),
                0 => new CheckResult("Off"),
                _ => throw new NotAvailableException(),
            }),

        new SimpleCheck("Check apps and files", () =>
        {
            int? policy = Dword(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "EnableSmartScreen");
            if (policy is not null)
                return CheckResult.OnOff(policy == 1);
            var local = RegistryHelper.Read(RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", "SmartScreenEnabled") as string;
            return CheckResult.OnOff(!string.Equals(local, "Off", StringComparison.OrdinalIgnoreCase));
        }),

        new SimpleCheck("SmartScreen for Microsoft Edge", () =>
        {
            int? policy = Dword(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Edge", "SmartScreenEnabled");
            int? user = Dword(RegistryHive.CurrentUser, @"Software\Microsoft\Edge\SmartScreenEnabled", "");
            return CheckResult.OnOff((policy ?? user ?? 1) == 1);
        }),

        new SimpleCheck("Phishing protection", () =>
        {
            int? policy = Dword(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WTDS\Components", "ServiceEnabled");
            int? local = Dword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\WTDS\Components", "ServiceEnabled");
            int? value = policy ?? local;
            if (value is null && Build < 22621)
                throw new NotAvailableException(); // Added in Windows 11 22H2.
            return CheckResult.OnOff((value ?? 1) == 1);
        }),

        new SimpleCheck("Potentially unwanted app blocking", () => Convert.ToInt32(MpPref("PUAProtection") ?? 0) switch
        {
            1 => new CheckResult("On"),
            2 => new CheckResult("Audit mode"),
            _ => new CheckResult("Off"),
        }),

        new SimpleCheck("SmartScreen for Microsoft Store apps", () =>
        {
            int? v = Dword(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\AppHost", "EnableWebContentEvaluation");
            return CheckResult.OnOff(v != 0);
        }),
    ];

    // ---------------- Exploit protection (system settings, part of App & browser control) ----------------

    [DllImport("kernel32.dll")]
    private static extern int GetSystemDEPPolicy();

    private const string KernelKey = @"SYSTEM\CurrentControlSet\Control\Session Manager\kernel";

    /// <summary>
    /// Reads one 2-bit field of the system MitigationOptions value.
    /// 0 = use default, 1 = always on, 2 = always off (3 = on, for Force randomization).
    /// </summary>
    private static ICheck Mitigation(string name, int shift, bool defaultOn) => new SimpleCheck(name, () =>
    {
        var raw = RegistryHelper.Read(RegistryHive.LocalMachine, KernelKey, "MitigationOptions") as byte[];
        ulong bits = 0;
        if (raw is not null)
            for (int i = 0; i < Math.Min(8, raw.Length); i++)
                bits |= (ulong)raw[i] << (8 * i);

        return ((bits >> shift) & 0x3) switch
        {
            1 or 3 => CheckResult.OnOff(true),
            2 => CheckResult.OnOff(false),
            _ => CheckResult.OnOff(defaultOn),
        };
    });

    public static ICheck[] ExploitProtection() =>
    [
        Mitigation("Control flow guard (CFG)", 40, defaultOn: true),
        // 0 = always off; opt-in, opt-out and always on all count as on.
        new SimpleCheck("Data Execution Prevention (DEP)", () => CheckResult.OnOff(GetSystemDEPPolicy() != 0)),
        Mitigation("Force randomization for images", 8, defaultOn: false),
        Mitigation("Randomize memory allocations", 16, defaultOn: true),
        Mitigation("High-entropy ASLR", 20, defaultOn: true),
        new SimpleCheck("Validate exception chains (SEHOP)", () =>
            CheckResult.OnOff(Dword(RegistryHive.LocalMachine, KernelKey, "DisableExceptionChainValidation") != 1)),
        Mitigation("Validate heap integrity", 12, defaultOn: true),
    ];

    // ---------------- Account protection ----------------

    public static ICheck[] AccountProtection() =>
    [
        new SimpleCheck("Dynamic lock", () => CheckResult.OnOff(
            Dword(RegistryHive.CurrentUser, @"Software\Microsoft\Windows NT\CurrentVersion\Winlogon", "EnableGoodbye") == 1)),
    ];

    // ---------------- Device security ----------------
    // TPM (security processor) and Secure Boot are reported at the top of the scan.

    private static object? DeviceGuard(string p) => WmiHelper.Get(DeviceGuardScope, "Win32_DeviceGuard", p);

    public static ICheck[] DeviceSecurity() =>
    [
        // 2 = running.
        new SimpleCheck("Core isolation (VBS)", () =>
            CheckResult.OnOff(Convert.ToInt32(DeviceGuard("VirtualizationBasedSecurityStatus") ?? 0) == 2)),

        // SecurityServicesRunning contains 2 when Hypervisor-enforced Code Integrity (Memory integrity) is running.
        new SimpleCheck("Memory integrity", () =>
            CheckResult.OnOff(DeviceGuard("SecurityServicesRunning") is uint[] running && running.Contains(2u))),

        new SimpleCheck("Local Security Authority protection", () =>
        {
            int? v = Dword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Lsa", "RunAsPPL");
            return CheckResult.OnOff(v is 1 or 2);
        }),

        new SimpleCheck("Microsoft Vulnerable Driver Blocklist", () =>
        {
            int? v = Dword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\CI\Config", "VulnerableDriverBlocklistEnable");
            // On by default from Windows 11 22H2 when the value is absent.
            return CheckResult.OnOff(v is not null ? v == 1 : Build >= 22621);
        }),

        new SimpleCheck("Kernel-mode hardware-enforced stack protection", () =>
        {
            int? v = Dword(RegistryHive.LocalMachine,
                @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\KernelShadowStacks", "Enabled");
            return CheckResult.OnOff(v == 1);
        }),
    ];
}
