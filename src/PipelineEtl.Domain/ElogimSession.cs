namespace PipelineEtl.Domain;

public sealed record ElogimSession(
    DateTime FechaSesion, string? IdEst, string? Identificacion, string? Bd, string? TipoConsulta);
