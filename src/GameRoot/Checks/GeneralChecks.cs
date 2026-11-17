using Microsoft.Win32;

namespace GameRoot.Checks;

/// <summary>Game Mode. Windows treats a missing value as on.</summary>
public sealed class GameModeCheck : ICheck
{
    public string Name => "Game Mode";

    public CheckResult Run()
    {
        int? v = RegistryHelper.ReadDword(RegistryHive.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled");
        return CheckResult.OnOff(v != 0);
    }
}

/// <summary>
/// Hardware-accelerated GPU scheduling (HwSchMode: 2 = on, 1 = off).
/// A missing value means the toggle has never been turned on, so it reads as off.
/// </summary>
public sealed class GpuSchedulingCheck : ICheck
{
    public string Name => "Hardware-accelerated GPU scheduling";

    public CheckResult Run()
    {
        int? v = RegistryHelper.ReadDword(RegistryHive.LocalMachine,
            @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode");
        return CheckResult.OnOff(v == 2);
    }
}

/// <summary>
/// Fast startup (fast boot). It only takes effect when HiberbootEnabled is 1 and
/// hibernation is available, so both must be on for this to read On.
/// </summary>
public sealed class FastStartupCheck : ICheck
{
    public string Name => "Fast Boot";

    public CheckResult Run()
    {
        int? hiberboot = RegistryHelper.ReadDword(RegistryHive.LocalMachine,
            @"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled");
        int? hibernate = RegistryHelper.ReadDword(RegistryHive.LocalMachine,
            @"SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled");

        return CheckResult.OnOff(hiberboot == 1 && hibernate != 0);
    }
}
