using RepairDesk.Core.Enums;

namespace RepairDesk.Services.PublicPortal;

/// <summary>
/// DTO público — apenas campos que o cliente final pode ver.
/// NUNCA inclui: custos internos, peças usadas, notas técnicas, outros
/// trabalhos do mesmo cliente, NIF/IBAN da loja, dados financeiros.
/// </summary>
public sealed record PublicRepairDto(
    string Slug,
    string EquipamentoPublico,
    string AvariaPublica,
    string? Diagnostico,
    PublicEstado Estado,
    DateTime EstadoSince,
    DateTime RecebidoEm,
    DateTime? EntregueEm,
    int? OrcamentoCents,
    bool OrcamentoAprovado,
    bool TemPrecoFinal,
    int? PrecoFinalCents,
    PublicLoja Loja,
    string ClientePrimeiroNome,
    IReadOnlyList<PublicTimelineEntry> Timeline,
    /// <summary>Health Score 0-100 do equipamento à entrada (se diagnóstico concluído).</summary>
    int? HealthScore,
    /// <summary>Pontos chave do diagnóstico (só items com Avaria/Marginal, label sem detalhe técnico).</summary>
    IReadOnlyList<string> DiagnosticoDestaques,
    /// <summary>Slug público da garantia (se já emitida). Frontend usa para `/g/{slug}`.</summary>
    string? GarantiaSlug,
    /// <summary>Já existe avaliação submetida (esconde o card "Como correu?").</summary>
    bool JaAvaliado,
    /// <summary>Fotos públicas (Antes/Durante/Depois marcadas como visíveis).</summary>
    IReadOnlyList<PublicFotoDto> Fotos,
    IReadOnlyList<PublicEquipmentFieldDto> CamposEquipamento,
    /// <summary>Sprint 88: cobertura por garantia de venda anterior, quando IMEI bate.</summary>
    PublicCoberturaGarantia? CoberturaGarantia);

/// <summary>
/// Indicação ao cliente de que esta reparação está coberta pela garantia da venda original.
/// Só exposto se a garantia da venda está activa. Slug permite ao cliente verificar.
/// </summary>
public sealed record PublicCoberturaGarantia(
    string GarantiaSlug,
    DateTime DataFimGarantia,
    int DiasRestantes);

public sealed record PublicEquipmentFieldDto(
    string Label,
    string? Value,
    int Ordem);

public sealed record PublicFotoDto(
    Guid Id,
    int Tipo,           // 0=Antes, 1=Durante, 2=Depois
    string? Legenda,
    DateTime CriadaEm);

public sealed record PublicLoja(
    string Nome,
    string? Telefone,
    string? Email,
    string? Website,
    string? LogoUrl);

public sealed record PublicTimelineEntry(
    PublicEstado Estado,
    DateTime MudouEm);

/// <summary>
/// Versão simplificada dos estados — linguagem client-friendly em vez de
/// jargão técnico ("Diagnóstico" → "Em análise").
/// </summary>
public enum PublicEstado
{
    Orcamento = 0,
    Recebido = 1,
    EmAnalise = 2,        // RepairStatus.Diagnostico / AguardaPeca / EmReparacao
    EmReparacao = 3,
    AguardaPeca = 4,
    Pronto = 5,
    Entregue = 6,
    Cancelado = 7,
}

public static class PublicEstadoMapper
{
    public static PublicEstado From(RepairStatus s) => s switch
    {
        RepairStatus.Orcamento => PublicEstado.Orcamento,
        RepairStatus.Recebido => PublicEstado.Recebido,
        RepairStatus.Diagnostico => PublicEstado.EmAnalise,
        RepairStatus.AguardaPeca => PublicEstado.AguardaPeca,
        RepairStatus.EmReparacao => PublicEstado.EmReparacao,
        RepairStatus.Pronto => PublicEstado.Pronto,
        RepairStatus.Entregue => PublicEstado.Entregue,
        RepairStatus.Cancelado => PublicEstado.Cancelado,
        _ => PublicEstado.EmAnalise,
    };
}

public sealed record AprovarOrcamentoRequest(bool Aceitar);

public sealed record PublicGarantiaDto(
    string Slug,
    string EquipamentoPublico,
    string Loja,
    string? LogoUrl,
    DateTime DataInicio,
    DateTime DataFim,
    int DiasGarantia,
    bool Activa,
    bool Anulada,
    int DiasRestantes,
    string? Cobertura,
    string? Exclusoes,
    /// <summary>"Reparacao" ou "Venda" — permite ao frontend mostrar UI diferente.</summary>
    string Origem,
    /// <summary>Numero ou referencia do documento de origem (Reparacao #123 ou Venda #45).</summary>
    string? DocumentoReferencia,
    /// <summary>Numero da fatura (quando origem é Venda e fatura foi emitida).</summary>
    string? NumeroFatura,
    /// <summary>Linhas da venda quando origem=Venda.</summary>
    IReadOnlyList<PublicGarantiaItemDto>? Items,
    /// <summary>Sprint 94: email da loja — para botão "Reclamar garantia" via mailto:</summary>
    string? LojaEmail,
    string? LojaTelefone);

public sealed record PublicGarantiaItemDto(
    string Descricao,
    int Quantidade,
    int PrecoUnitarioCents,
    int TotalCents,
    /// <summary>IMEI mascarado (357XXXXXXX0001) — anti-fraude no portal público.</summary>
    string? ImeiMascarado);

public sealed record SubmitAvaliacaoRequest(int Score, string? Comentario, bool PublicarTestemunho);

public sealed record AvaliacaoSubmittedDto(
    int Score,
    string? Comentario,
    /// <summary>Se score 4-5, URL do Google Reviews da loja (se configurada).</summary>
    string? GoogleReviewUrl);
