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
        // Sprint 563: também ES/EN ("Cristal Templado", "Tempered Glass") — fornecedores espanhóis e NL.
        ["vidro", "pelicula", "película", "temperado", "templado", "tempered", "hidrogel", "protetor", "protector", "capa "];

    /// <summary>
    /// 18/11/2026 (Bruno): já não há lucro por defeito (antes 1 € películas / 5 € resto) — o Bruno mete o lucro ou o
    /// preço de venda (do artigo) à mão. Fica 0 até ele definir.
    /// </summary>
    public static decimal LucroPorDefeito(string descricao) => 0m;
}
