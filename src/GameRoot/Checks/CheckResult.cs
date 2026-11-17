namespace GameRoot.Checks;

/// <summary>The outcome of one check: the value to display.</summary>
public sealed record CheckResult(string Value)
{
    public static CheckResult OnOff(bool on) => new(on ? "On" : "Off");
}
