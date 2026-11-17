using Microsoft.Win32;

namespace GameRoot.Checks;

/// <summary>
/// Reports whether UEFI Secure Boot is on. Windows records the state in the registry,
/// readable without administrator rights. A missing value means the PC booted in
/// legacy BIOS mode, where Secure Boot is off.
/// </summary>
public sealed class SecureBootCheck : ICheck
{
    public string Name => "Secure Boot";

    public CheckResult Run()
    {
        int? state = RegistryHelper.ReadDword(
            RegistryHive.LocalMachine,
            @"SYSTEM\CurrentControlSet\Control\SecureBoot\State",
            "UEFISecureBootEnabled");

        return CheckResult.OnOff(state == 1);
    }
}
