namespace RepairDesk.Core.Abstractions;

/// <summary>
/// Sprint 561: ordenação pedida por uma lista (clicar no cabeçalho da coluna). <see cref="Campo"/> é um nome
/// curto conhecido de cada lista ("numero", "data", "total"…); desconhecido = ordem por defeito da lista.
/// </summary>
public sealed record Ordenacao(string? Campo, bool Desc)
{
    public static Ordenacao? De(string? campo, string? dir)
        => string.IsNullOrWhiteSpace(campo) ? null : new Ordenacao(campo.Trim().ToLowerInvariant(), !string.Equals(dir, "asc", StringComparison.OrdinalIgnoreCase));
}
