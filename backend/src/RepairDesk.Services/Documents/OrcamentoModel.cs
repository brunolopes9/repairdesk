namespace RepairDesk.Services.Documents;

public sealed record OrcamentoEmissor(
    string Nome,
    string? Nif,
    string? Morada,
    string? CodigoPostal,
    string? Localidade,
    string? Telefone,
    string? Email,
    string? Website,
    string? Iban,
    string? CaePrincipal,
    string? CaeSecundarios,
    string? LogoUrl,
    string? PrimaryColor,
    string? TermosCondicoes);

public sealed record OrcamentoCliente(
    string Nome,
    string? Telefone,
    string? Email,
    string? Nif);

public sealed record OrcamentoLinha(
    string Descricao,
    int ValorCents);

public sealed record OrcamentoCampoEquipamento(
    string Label,
    string Value);

public sealed record OrcamentoData(
    string Numero,
    string Tipo,             // ex: "Reparação", "Trabalho"
    DateTime Data,
    DateTime ValidoAte,
    OrcamentoEmissor Emissor,
    OrcamentoCliente Cliente,
    string Titulo,           // ex: "Substituição de ecrã iPhone 13"
    string? Descricao,
    IReadOnlyList<OrcamentoLinha> Linhas,
    int TotalCents,
    string? Observacoes,
    IReadOnlyList<OrcamentoCampoEquipamento>? CamposEquipamento = null,
    string? PortalUrl = null);
