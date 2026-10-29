namespace UAI.ZeroVP.DTO;

/// <summary>
/// Informace o běžící instanci API.
/// Ve výuce Dockeru slouží k tomu, aby bylo vidět, která replika kontejneru
/// požadavek obsloužila a že každá má vlastní (prázdnou) paměť.
/// </summary>
public sealed record InstanceInfoDto
{
    /// <summary>Jednoznačný identifikátor instance (z konfigurace InstanceId, jinak kombinace stroje a PID).</summary>
    /// <example>instance-1</example>
    public required string InstanceId { get; init; }

    /// <summary>Jméno stroje – v kontejneru odpovídá jeho hostname (zkrácené ID kontejneru).</summary>
    /// <example>zerovp-app-6d8b9</example>
    public required string MachineName { get; init; }

    /// <summary>Identifikátor běžícího procesu (PID) – odliší více lokálních instancí na stejném stroji.</summary>
    /// <example>12345</example>
    public int ProcessId { get; init; }

    /// <summary>Název prostředí ASP.NET Core (Development / Production / ...).</summary>
    /// <example>Development</example>
    public required string Environment { get; init; }

    /// <summary>Verze sestavení API.</summary>
    /// <example>1.0.0</example>
    public required string Version { get; init; }

    /// <summary>Aktuální čas instance v UTC.</summary>
    /// <example>2026-10-12T12:00:00Z</example>
    public required DateTimeOffset UtcNow { get; init; }

    /// <summary>Doba běhu procesu – po restartu kontejneru se vynuluje.</summary>
    /// <example>01:23:45</example>
    public required TimeSpan Uptime { get; init; }

    /// <summary>Počet úkolů, které má tato instance právě v paměti.</summary>
    /// <example>3</example>
    public required int TodoCount { get; init; }
}
