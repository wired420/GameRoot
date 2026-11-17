namespace GameRoot.Checks;

/// <summary>A read-only check of one Windows setting. Checks never change anything.</summary>
public interface ICheck
{
    string Name { get; }
    CheckResult Run();
}
