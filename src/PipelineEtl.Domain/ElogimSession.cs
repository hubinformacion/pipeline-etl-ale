namespace PipelineEtl.Domain;

public sealed record ElogimSession(
    DateTime FechaSesion, string? UserEmail, string? Identificacion, string? Bd, string? TipoConsulta);
