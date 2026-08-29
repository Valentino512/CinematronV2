namespace Cinematron.Models;

public sealed record DatabaseDiagnosticsViewModel
{
    public string EnvironmentName { get; init; } = string.Empty;

    public string Provider { get; init; } = string.Empty;

    public string Server { get; init; } = string.Empty;

    public string Database { get; init; } = string.Empty;

    public string RedactedConnectionString { get; init; } = string.Empty;

    public bool CanConnect { get; init; }

    public string? ConnectionError { get; init; }

    public IReadOnlyList<string> PendingMigrations { get; init; } = Array.Empty<string>();
}
