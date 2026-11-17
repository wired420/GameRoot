using System.Runtime.InteropServices;

namespace GameRoot.Checks;

/// <summary>
/// Reports whether a TPM is enabled and visible to Windows. Uses the TPM Base Services
/// API (tbs.dll), which works without administrator rights.
/// </summary>
public sealed class TpmCheck : ICheck
{
    public string Name => "TPM";

    [StructLayout(LayoutKind.Sequential)]
    private struct TPM_DEVICE_INFO
    {
        public uint structVersion;
        public uint tpmVersion;
        public uint tpmInterfaceType;
        public uint tpmImpRevision;
    }

    private const uint TBS_SUCCESS = 0;

    [DllImport("tbs.dll")]
    private static extern uint Tbsi_GetDeviceInfo(uint size, out TPM_DEVICE_INFO info);

    public CheckResult Run()
    {
        try
        {
            uint rc = Tbsi_GetDeviceInfo((uint)Marshal.SizeOf<TPM_DEVICE_INFO>(), out var info);
            if (rc != TBS_SUCCESS)
                return CheckResult.OnOff(false);

            return CheckResult.OnOff(true);
        }
        catch (DllNotFoundException)
        {
            return CheckResult.OnOff(false);
        }
        catch (EntryPointNotFoundException)
        {
            return CheckResult.OnOff(false);
        }
    }
}
