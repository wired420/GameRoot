namespace GameRoot.Checks;

/// <summary>A read-only check of one Windows setting. Checks never change anything.</summary>
public interface ICheck
{
    string Name { get; }
    CheckResult Run();
}

/// <summary>
/// A check whose setting the app can also change. Only non-security Windows settings
/// implement this; the Windows Security protections stay read-only on purpose.
/// </summary>
public interface IToggle
{
    /// <summary>Turns the setting on or off. May throw if it can't be written.</summary>
    void Set(bool on);

    /// <summary>True when the change only takes full effect after a restart.</summary>
    bool NeedsReboot { get; }
}
