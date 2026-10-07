namespace RepairDesk.Services.Fiscal;

/// <summary>
/// Valores por defeito enquanto o perfil fiscal do tenant (Doc 94, Fase 6) não existe.
/// Centralizado aqui para haver UM sítio a mudar quando o perfil passar a dar estes valores.
/// </summary>
public static class FiscalDefaults
{
    /// <summary>Taxa normal de IVA em Portugal continental (CIVA art. 18.º n.º 1 c)).</summary>
    public const decimal TaxaIvaNormal = 0.23m;

    public const decimal LucroPecaPorDefeito = 5m;
    public const decimal LucroAcessorioPorDefeito = 1m;

    private static readonly string[] PalavrasAcessorio =
        ["vidro", "pelicula", "película", "temperado", "hidrogel", "protetor", "protector", "capa "];

    /// <summary>Lucro por unidade sugerido (SPEC §1): 1 € em películas/vidros, 5 € no resto. Sempre editável.</summary>
    public static decimal LucroPorDefeito(string descricao)
    {
        var d = (descricao ?? string.Empty).ToLowerInvariant();
        return PalavrasAcessorio.Any(d.Contains) ? LucroAcessorioPorDefeito : LucroPecaPorDefeito;
    }
}
