using Microsoft.Extensions.Logging;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Billing;
using RepairDesk.Services.Trabalhos;

namespace RepairDesk.Services.Avencas;

public interface IAvencaService
{
    Task<IReadOnlyList<AvencaDto>> ListAsync(Guid? clienteId, CancellationToken ct = default);
    Task<AvencaDto> CreateAsync(SaveAvencaRequest req, CancellationToken ct = default);
    Task<AvencaDto> UpdateAsync(Guid id, SaveAvencaRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    /// <summary>Emite o período devido: cria o Trabalho do mês + Fatura (FT) Moloni e avança a ProximaEmissao.</summary>
    Task<AvencaEmissaoResult> EmitirAsync(Guid id, CancellationToken ct = default);
}

public sealed record SaveAvencaRequest(
    Guid ClienteId,
    string Descricao,
    int ValorCents,
    decimal IvaRate,
    JobCategory Categoria,
    int PeriodicidadeMeses,
    DateTime ProximaEmissao,
    bool Ativa = true,
    string? Notas = null);

public sealed record AvencaDto(
    Guid Id,
    Guid ClienteId,
    string? ClienteNome,
    string Descricao,
    int ValorCents,
    decimal IvaRate,
    JobCategory Categoria,
    int PeriodicidadeMeses,
    DateTime ProximaEmissao,
    bool Ativa,
    string? Notas,
    DateTime? UltimaEmissaoEm,
    Guid? UltimoTrabalhoId,
    // true quando a próxima emissão já está devida (hoje ≥ ProximaEmissao) e a avença está ativa.
    bool Devida);

public sealed record AvencaEmissaoResult(
    AvencaDto Avenca,
    Guid TrabalhoId,
    string? InvoiceNumber);

public sealed class AvencaService : IAvencaService
{
    private readonly IAvencaRepository _repo;
    private readonly IClienteRepository _clientes;
    private readonly ITrabalhoService _trabalhos;
    private readonly IBillingProvider _billing;
    private readonly ILogger<AvencaService> _logger;

    public AvencaService(
        IAvencaRepository repo,
        IClienteRepository clientes,
        ITrabalhoService trabalhos,
        IBillingProvider billing,
        ILogger<AvencaService> logger)
    {
        _repo = repo;
        _clientes = clientes;
        _trabalhos = trabalhos;
        _billing = billing;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AvencaDto>> ListAsync(Guid? clienteId, CancellationToken ct = default)
        => (await _repo.ListAsync(clienteId, ct)).Select(ToDto).ToList();

    public async Task<AvencaDto> CreateAsync(SaveAvencaRequest req, CancellationToken ct = default)
    {
        await ValidateAsync(req, ct);
        var avenca = new Avenca
        {
            ClienteId = req.ClienteId,
            Descricao = req.Descricao.Trim(),
            ValorCents = req.ValorCents,
            IvaRate = req.IvaRate,
            Categoria = req.Categoria,
            PeriodicidadeMeses = Math.Clamp(req.PeriodicidadeMeses, 1, 24),
            ProximaEmissao = req.ProximaEmissao.Date,
            Ativa = req.Ativa,
            Notas = req.Notas?.Trim(),
        };
        await _repo.AddAsync(avenca, ct);
        await _repo.SaveAsync(ct);
        return ToDto(await _repo.FindByIdAsync(avenca.Id, ct) ?? avenca);
    }

    public async Task<AvencaDto> UpdateAsync(Guid id, SaveAvencaRequest req, CancellationToken ct = default)
    {
        await ValidateAsync(req, ct);
        var avenca = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Avenca", id);
        avenca.ClienteId = req.ClienteId;
        avenca.Descricao = req.Descricao.Trim();
        avenca.ValorCents = req.ValorCents;
        avenca.IvaRate = req.IvaRate;
        avenca.Categoria = req.Categoria;
        avenca.PeriodicidadeMeses = Math.Clamp(req.PeriodicidadeMeses, 1, 24);
        avenca.ProximaEmissao = req.ProximaEmissao.Date;
        avenca.Ativa = req.Ativa;
        avenca.Notas = req.Notas?.Trim();
        await _repo.SaveAsync(ct);
        return ToDto(avenca);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var avenca = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Avenca", id);
        _repo.Remove(avenca); // soft-delete via interceptor
        await _repo.SaveAsync(ct);
    }

    public async Task<AvencaEmissaoResult> EmitirAsync(Guid id, CancellationToken ct = default)
    {
        var avenca = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Avenca", id);
        if (!avenca.Ativa)
            throw new ValidationException("avenca_inativa", "Esta avença está inativa — ativa-a antes de emitir.");

        var periodo = avenca.ProximaEmissao;

        // 1) Cria o Trabalho do período (o "recibo mensal" da avença vive como Trabalho normal:
        //    fatura, dívida, recibo, extrato — tudo pelo pipeline existente).
        var trabalho = await _trabalhos.CreateAsync(new CreateTrabalhoRequest(
            ClienteId: avenca.ClienteId,
            Titulo: $"{avenca.Descricao} — {periodo:MM/yyyy}",
            Descricao: $"Avença ({PeriodicidadeLabel(avenca.PeriodicidadeMeses)}). Período {periodo:MM/yyyy}.",
            Categoria: avenca.Categoria,
            OrcamentoCents: avenca.ValorCents,
            Notas: avenca.Notas), ct);

        // 2) O período fica CONSUMIDO assim que o Trabalho existe — mesmo que a emissão Moloni
        //    falhe a seguir, o retry é na ficha do Trabalho (botão Emitir), NÃO aqui; senão um
        //    retry da avença criava um segundo Trabalho do mesmo mês (duplicado).
        avenca.ProximaEmissao = periodo.AddMonths(avenca.PeriodicidadeMeses);
        avenca.UltimaEmissaoEm = DateTime.UtcNow;
        avenca.UltimoTrabalhoId = trabalho.Id;
        await _repo.SaveAsync(ct);

        // 3) Emite a Fatura a crédito (FT) — entra no ciclo dívida→push→recibo (S537/S544/S545).
        string? invoiceNumber = null;
        try
        {
            var invoice = await _billing.EmitTrabalhoInvoiceAsync(
                trabalho.Id, avenca.IvaRate, null, BillingDocumentType.Fatura, ct);
            invoiceNumber = invoice.Number;
        }
        catch (Exception ex)
        {
            // Compensation: preferimos Trabalho criado SEM fatura a estado inconsistente. O caller
            // mostra o aviso e o Bruno emite da ficha do Trabalho quando o Moloni voltar.
            _logger.LogWarning(ex, "Avença {Id}: Trabalho {TrabalhoId} criado mas emissão Moloni falhou.", id, trabalho.Id);
            throw new ValidationException(
                "avenca_emissao_parcial",
                $"O Trabalho do período {periodo:MM/yyyy} foi criado, mas a fatura Moloni falhou: {ex.Message} " +
                "Abre a ficha do Trabalho e emite a fatura de lá (a avença já avançou para o próximo período).");
        }

        return new AvencaEmissaoResult(ToDto(avenca), trabalho.Id, invoiceNumber);
    }

    private async Task ValidateAsync(SaveAvencaRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Descricao))
            throw new ValidationException("avenca_descricao", "A descrição é obrigatória.");
        if (req.ValorCents <= 0)
            throw new ValidationException("avenca_valor", "O valor tem de ser positivo.");
        if (req.IvaRate is < 0 or > 23)
            throw new ValidationException("avenca_iva", "Taxa de IVA inválida (0, 6, 13 ou 23).");
        _ = await _clientes.FindByIdAsync(req.ClienteId, ct)
            ?? throw new ValidationException("avenca_cliente", "Cliente não encontrado.");
    }

    private static string PeriodicidadeLabel(int meses) => meses switch
    {
        1 => "mensal",
        3 => "trimestral",
        12 => "anual",
        _ => $"a cada {meses} meses",
    };

    private static AvencaDto ToDto(Avenca a) => new(
        a.Id, a.ClienteId, a.Cliente?.Nome, a.Descricao, a.ValorCents, a.IvaRate, a.Categoria,
        a.PeriodicidadeMeses, a.ProximaEmissao, a.Ativa, a.Notas, a.UltimaEmissaoEm, a.UltimoTrabalhoId,
        Devida: a.Ativa && a.ProximaEmissao.Date <= DateTime.UtcNow.Date);
}
