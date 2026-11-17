using Microsoft.Win32;

namespace GameRoot.Checks;

internal static class RegistryHelper
{
    /// <summary>Reads a value from the 64-bit registry view. Returns null when the key or value is missing.</summary>
    public static object? Read(RegistryHive hive, string subKey, string valueName)
    {
        using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        using var key = root.OpenSubKey(subKey);
        return key?.GetValue(valueName);
    }

    public static int? ReadDword(RegistryHive hive, string subKey, string valueName) =>
        Read(hive, subKey, valueName) is int i ? i : null;

    /// <summary>Writes a DWORD to the 64-bit registry view, creating the key if needed.</summary>
    public static void WriteDword(RegistryHive hive, string subKey, string valueName, int value)
    {
        using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        using var key = root.CreateSubKey(subKey, writable: true)
            ?? throw new InvalidOperationException($"Could not open {subKey} for writing.");
        key.SetValue(valueName, value, RegistryValueKind.DWord);
    }
}
