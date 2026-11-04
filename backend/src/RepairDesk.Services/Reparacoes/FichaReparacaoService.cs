using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Documents;

namespace RepairDesk.Services.Reparacoes;

/// <summary>Um teste da checklist: "ok", "falha" ou "nt" (não testado / não liga), à entrada e à saída.</summary>
public sealed record ChecklistItemDto(string Chave, string Label, string? Entrada, string? Saida);

public sealed record FichaReparacaoDto(
    string? Imei,
    SenhaTipo SenhaTipo,
    /// <summary>A senha em claro — só para a equipa (nunca no portal). Null depois de entregue.</summary>
    string? Senha,
    bool? PermiteTestes,
    string? EstadoFisico,
    string? Acessorios,
    IReadOnlyList<ChecklistItemDto> Checklist,
    DateTime? ClienteAssinouEm,
    bool ClienteAssinouPeloLink,
    DateTime? TecnicoAssinouEm,
    /// <summary>Link para o cliente ver a ordem e assinar no telemóvel dele.</summary>
    string? LinkAssinatura);

public sealed record FichaReparacaoWriteRequest(
    string? Imei,
    SenhaTipo SenhaTipo,
    string? Senha,
    bool? PermiteTestes,
    string? EstadoFisico,
    string? Acessorios,
    IReadOnlyList<ChecklistItemDto>? Checklist);

/// <summary>A ordem de reparação como o cliente a vê no link (sem a senha).</summary>
public sealed record PublicFichaDto(
    string Numero,
    string Loja,
    string? LojaContacto,
    string Cliente,
    string Equipamento,
    string? Imei,
    string Avaria,
    SenhaTipo SenhaTipo,
    bool? PermiteTestes,
    string? EstadoFisico,
    string? Acessorios,
    IReadOnlyList<ChecklistItemDto> Checklist,
    IReadOnlyList<PublicFichaLinha> Reparacao,
    int TotalCents,
    DateTime RecebidoEm,
    DateTime? PrevistoPara,
    string? Termos,
    string Aviso,
    DateTime? AssinadaEm,
    bool PodeAssinar);

public sealed record PublicFichaLinha(string Descricao, int TotalCents);

public sealed record AssinarFichaRequest(string DataUrl, bool AceitaTermos);

public interface IFichaReparacaoService
{
    Task<FichaReparacaoDto> GetAsync(Guid vendaId, CancellationToken ct = default);
    Task<FichaReparacaoDto> SaveAsync(Guid vendaId, FichaReparacaoWriteRequest req, CancellationToken ct = default);
    Task<(byte[] Pdf, string Nome)> OrdemPdfAsync(Guid vendaId, CancellationToken ct = default);
    Task<PublicFichaDto> PublicAsync(string slug, CancellationToken ct = default);
    Task<PublicFichaDto> AssinarPublicoAsync(string slug, AssinarFichaRequest req, CancellationToken ct = default);
    Task<(byte[] Pdf, string Nome)> PublicPdfAsync(string slug, CancellationToken ct = default);
    /// <summary>Apaga a senha guardada (ao entregar ou cancelar). Não grava — fica na unidade de trabalho de quem chama.</summary>
    Task LimparSenhaAsync(Guid vendaId, CancellationToken ct = default);
}

/// <summary>
/// Sprint 595: a ordem de reparação digital. A equipa preenche a ficha (IMEI, senha, testes, estado, acessórios,
/// checklist); o cliente vê-a e assina no link público (/r/{slug}) ou no balcão; o técnico também assina. Imprime-se
/// em A4 com 2 vias (loja / cliente) e uma 2.ª folha com a checklist, também em 2 vias.
///
/// A senha é cifrada, nunca vai para o portal nem para a via do cliente, e apaga-se ao entregar/cancelar.
/// </summary>
public sealed partial class FichaReparacaoService : IFichaReparacaoService
{
    /// <summary>Testes por defeito (telemóveis/tablets), pela ordem da folha.</summary>
    public static readonly (string Chave, string Label)[] Testes =
    [
        ("liga", "Liga / arranca"),
        ("ecra", "Ecrã (imagem)"),
        ("touch", "Touch"),
        ("camara_traseira", "Câmara traseira"),
        ("camara_frontal", "Câmara frontal"),
        ("altifalante", "Altifalante"),
        ("auricular", "Auricular (chamadas)"),
        ("microfone", "Microfone"),
        ("carga", "Conector de carga"),
        ("bateria", "Bateria"),
        ("botoes", "Botões (power / volume)"),
        ("wifi", "Wi-Fi"),
        ("bluetooth", "Bluetooth"),
        ("rede", "Rede móvel / SIM"),
        ("biometria", "Face ID / impressão digital"),
        ("vibracao", "Vibração"),
        ("proximidade", "Sensor de proximidade"),
        ("gps", "GPS"),
    ];

    /// <summary>Aviso no rodapé da via do cliente.</summary>
    public const string AvisoLevantamento =
        "Atenção: o equipamento deve ser levantado até 30 dias depois de avisarmos que está pronto. Depois desse prazo " +
        "pode ser cobrado o armazenamento e, ao fim de 90 dias, o equipamento é considerado abandonado — pode ser vendido " +
        "pelo valor da reparação ou encaminhado para reciclagem. Ao assinar esta ordem, aceita estes termos.";

    private static readonly string[] Estados = ["ok", "falha", "nt"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IVendaRepository _vendas;
    private readonly IVendaFichaRepository _fichas;
    private readonly IVendaAssinaturaRepository _assinaturas;
    private readonly ITenantRepository _tenants;
    private readonly ISecretProtector _secrets;
    private readonly IAuditLogger _audit;
    private readonly IConfiguration _config;

    public FichaReparacaoService(IVendaRepository vendas, IVendaFichaRepository fichas, IVendaAssinaturaRepository assinaturas,
        ITenantRepository tenants, ISecretProtector secrets, IAuditLogger audit, IConfiguration config)
    {
        _vendas = vendas;
        _fichas = fichas;
        _assinaturas = assinaturas;
        _tenants = tenants;
        _secrets = secrets;
        _audit = audit;
        _config = config;
    }

    // ---------- equipa ----------

    public async Task<FichaReparacaoDto> GetAsync(Guid vendaId, CancellationToken ct = default)
    {
        var venda = await ReparacaoAsync(vendaId, ct);
        var ficha = await _fichas.FindByVendaAsync(venda.Id, ct);
        return await DtoAsync(venda, ficha, ct);
    }

    public async Task<FichaReparacaoDto> SaveAsync(Guid vendaId, FichaReparacaoWriteRequest req, CancellationToken ct = default)
    {
        var venda = await ReparacaoAsync(vendaId, ct);
        var ficha = await _fichas.FindByVendaAsync(venda.Id, ct);
        if (ficha is null)
        {
            ficha = new VendaFicha { TenantId = venda.TenantId, VendaId = venda.Id };
            await _fichas.AddAsync(ficha, ct);
        }

        ficha.Imei = Limpo(req.Imei, 20);
        ficha.SenhaTipo = req.SenhaTipo;
        var senha = ValidarSenha(req.SenhaTipo, req.Senha);
        // Depois de entregue/cancelada não se volta a guardar a senha.
        ficha.SenhaCifrada = senha is null || venda.Estado is VendaEstado.Entregue or VendaEstado.Cancelada ? null : _secrets.Protect(senha);
        ficha.PermiteTestes = req.PermiteTestes;
        ficha.EstadoFisico = Limpo(req.EstadoFisico, 1000);
        ficha.Acessorios = Limpo(req.Acessorios, 500);
        ficha.ChecklistJson = JsonSerializer.Serialize(Normalizar(req.Checklist), Json);

        await _fichas.SaveAsync(ct);
        // Sem a senha no registo de auditoria.
        await _audit.LogAsync(AuditAction.Update, nameof(VendaFicha), ficha.Id, new { venda.Numero, senhaTipo = req.SenhaTipo.ToString(), req.PermiteTestes }, ct: ct);
        return await DtoAsync(venda, ficha, ct);
    }

    public async Task<(byte[] Pdf, string Nome)> OrdemPdfAsync(Guid vendaId, CancellationToken ct = default)
    {
        var venda = await ReparacaoAsync(vendaId, ct);
        return (await RenderAsync(venda, soCliente: false, ct), $"Ordem_R-{venda.Numero:D5}.pdf");
    }

    public async Task LimparSenhaAsync(Guid vendaId, CancellationToken ct = default)
    {
        var ficha = await _fichas.FindByVendaAsync(vendaId, ct);
        if (ficha is not null) ficha.SenhaCifrada = null;
    }

    // ---------- link público ----------

    public async Task<PublicFichaDto> PublicAsync(string slug, CancellationToken ct = default)
    {
        var venda = await PorSlugAsync(slug, ct);
        return await PublicDtoAsync(venda, ct);
    }

    public async Task<PublicFichaDto> AssinarPublicoAsync(string slug, AssinarFichaRequest req, CancellationToken ct = default)
    {
        var venda = await PorSlugAsync(slug, ct);
        if (!req.AceitaTermos)
            throw new ValidationException("termos_nao_aceites", "Para assinar tem de confirmar que leu e aceita a ordem de reparação.");
        if (venda.Estado is VendaEstado.Entregue or VendaEstado.Cancelada)
            throw new ConflictException("reparacao_fechada", "Esta reparação já está fechada.");
        if (await _assinaturas.FindAsync(venda.Id, AssinaturaTipo.Entrada, ct) is not null)
            throw new ConflictException("ja_assinada", "Esta ordem de reparação já está assinada.");

        var png = AssinaturaService.DecodePng(req.DataUrl);
        await _assinaturas.AddAsync(new VendaAssinatura
        {
            TenantId = venda.TenantId, VendaId = venda.Id, Tipo = AssinaturaTipo.Entrada, PngBytes = png,
            AssinadaEm = DateTime.UtcNow, PeloLink = true,
        }, ct);
        await _assinaturas.SaveAsync(ct);
        return await PublicDtoAsync(venda, ct);
    }

    public async Task<(byte[] Pdf, string Nome)> PublicPdfAsync(string slug, CancellationToken ct = default)
    {
        var venda = await PorSlugAsync(slug, ct);
        return (await RenderAsync(venda, soCliente: true, ct), $"Ordem_R-{venda.Numero:D5}.pdf");
    }

    // ---------- regras ----------

    private async Task<Venda> ReparacaoAsync(Guid vendaId, CancellationToken ct)
    {
        var venda = await _vendas.FindByIdWithItemsAsync(vendaId, ct) ?? throw new NotFoundException("Venda", vendaId);
        if (venda.Tipo != VendaTipo.Reparacao)
            throw new ValidationException("nao_e_reparacao", "A ordem de reparação só existe para reparações.");
        return venda;
    }

    private async Task<Venda> PorSlugAsync(string slug, CancellationToken ct)
    {
        var venda = await _vendas.FindByPublicSlugAsync(slug.Trim(), ct) ?? throw new NotFoundException("Reparacao", slug);
        if (venda.Tipo != VendaTipo.Reparacao || venda.CreatedAt < DateTime.UtcNow.AddYears(-2))
            throw new NotFoundException("Reparacao", slug);
        return venda;
    }

    private async Task<FichaReparacaoDto> DtoAsync(Venda venda, VendaFicha? ficha, CancellationToken ct)
    {
        var assinaturas = await _assinaturas.ListByVendaAsync(venda.Id, ct);
        var cliente = assinaturas.FirstOrDefault(a => a.Tipo == AssinaturaTipo.Entrada);
        var tecnico = assinaturas.FirstOrDefault(a => a.Tipo == AssinaturaTipo.Tecnico);
        return new FichaReparacaoDto(
            ficha?.Imei, ficha?.SenhaTipo ?? SenhaTipo.Nenhuma, Senha(ficha), ficha?.PermiteTestes, ficha?.EstadoFisico, ficha?.Acessorios,
            Checklist(ficha), cliente?.AssinadaEm, cliente?.PeloLink ?? false, tecnico?.AssinadaEm,
            EntradaPdfService.PortalUrl(_config, venda.PublicSlug));
    }

    private async Task<PublicFichaDto> PublicDtoAsync(Venda venda, CancellationToken ct)
    {
        var ficha = await _fichas.FindByVendaAsync(venda.Id, ct);
        var tenant = await _tenants.FindByIdAsync(venda.TenantId, ct);
        var assinatura = await _assinaturas.FindAsync(venda.Id, AssinaturaTipo.Entrada, ct);
        return new PublicFichaDto(
            $"R-{venda.Numero:D5}", tenant?.LegalName ?? tenant?.Name ?? "Loja", tenant?.Phone,
            PrimeiroNome(venda.Cliente?.Nome), venda.Equipamento ?? "Equipamento", ficha?.Imei, venda.Problema ?? "—",
            ficha?.SenhaTipo ?? SenhaTipo.Nenhuma, ficha?.PermiteTestes, ficha?.EstadoFisico, ficha?.Acessorios,
            Checklist(ficha).Where(c => c.Entrada is not null).ToList(),
            venda.Items.OrderBy(i => i.CreatedAt).Select(i => new PublicFichaLinha(i.Descricao, i.TotalCents)).ToList(),
            venda.TotalCents, venda.CreatedAt, venda.PrevistoPara, tenant?.TermosCondicoes, AvisoLevantamento,
            assinatura?.AssinadaEm,
            assinatura is null && venda.Estado is not (VendaEstado.Entregue or VendaEstado.Cancelada));
    }

    private async Task<byte[]> RenderAsync(Venda venda, bool soCliente, CancellationToken ct)
    {
        var ficha = await _fichas.FindByVendaAsync(venda.Id, ct);
        var tenant = await _tenants.FindByIdAsync(venda.TenantId, ct);
        var assinaturas = await _assinaturas.ListByVendaAsync(venda.Id, ct);
        var c = venda.Cliente;
        var morada = string.Join(", ", new[] { c?.Morada, string.Join(" ", new[] { c?.CodigoPostal, c?.Localidade }.Where(s => !string.IsNullOrWhiteSpace(s))) }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        var data = new OrdemReparacaoData(
            $"R-{venda.Numero:D5}", PdfEmissor.From(tenant), c?.Nome ?? "—", c?.Telefone, morada.Length == 0 ? null : morada, c?.Nif,
            venda.CreatedAt, venda.PrevistoPara, venda.Estado == VendaEstado.Entregue ? venda.Data : null,
            venda.Equipamento ?? "—", ficha?.Imei, venda.Problema ?? "—",
            ficha?.SenhaTipo ?? SenhaTipo.Nenhuma, soCliente ? null : Senha(ficha), ficha?.PermiteTestes, ficha?.EstadoFisico, ficha?.Acessorios,
            venda.Items.OrderBy(i => i.CreatedAt).Select(i => new OrcamentoLinha(i.Descricao, i.TotalCents)).ToList(), venda.TotalCents,
            Checklist(ficha), tenant?.TermosCondicoes, AvisoLevantamento,
            assinaturas.FirstOrDefault(a => a.Tipo == AssinaturaTipo.Entrada) is { } ac ? new OrdemAssinatura(ac.PngBytes, ac.AssinadaEm, ac.PeloLink) : null,
            assinaturas.FirstOrDefault(a => a.Tipo == AssinaturaTipo.Tecnico) is { } at ? new OrdemAssinatura(at.PngBytes, at.AssinadaEm, false) : null,
            EntradaPdfService.PortalUrl(_config, venda.PublicSlug));
        return OrdemReparacaoPdfRenderer.Render(data, soCliente);
    }

    private string? Senha(VendaFicha? f)
    {
        if (f?.SenhaCifrada is null) return null;
        try { return _secrets.Unprotect(f.SenhaCifrada); }
        catch { return null; }
    }

    internal static IReadOnlyList<ChecklistItemDto> Checklist(VendaFicha? ficha)
    {
        var guardados = new Dictionary<string, ChecklistItemDto>();
        if (ficha is not null)
        {
            try
            {
                foreach (var i in JsonSerializer.Deserialize<List<ChecklistItemDto>>(ficha.ChecklistJson, Json) ?? [])
                    guardados[i.Chave] = i;
            }
            catch (JsonException) { /* ficha antiga/estragada — começa vazia */ }
        }
        return Testes.Select(t => guardados.TryGetValue(t.Chave, out var g)
            ? new ChecklistItemDto(t.Chave, t.Label, g.Entrada, g.Saida)
            : new ChecklistItemDto(t.Chave, t.Label, null, null)).ToList();
    }

    private static List<ChecklistItemDto> Normalizar(IReadOnlyList<ChecklistItemDto>? itens)
    {
        var conhecidos = Testes.ToDictionary(t => t.Chave, t => t.Label);
        return (itens ?? [])
            .Where(i => conhecidos.ContainsKey(i.Chave))
            .Select(i => new ChecklistItemDto(i.Chave, conhecidos[i.Chave], Estado(i.Entrada), Estado(i.Saida)))
            .Where(i => i.Entrada is not null || i.Saida is not null)
            .ToList();
    }

    private static string? Estado(string? e) => e is null ? null
        : Estados.Contains(e.Trim().ToLowerInvariant()) ? e.Trim().ToLowerInvariant()
        : throw new ValidationException("checklist_invalida", "Cada teste é ok, falha ou nt (não testado).");

    private static string? ValidarSenha(SenhaTipo tipo, string? senha)
    {
        var s = senha?.Trim();
        if (tipo == SenhaTipo.Nenhuma || string.IsNullOrEmpty(s)) return null;
        return tipo switch
        {
            SenhaTipo.Pin when !PinRegex().IsMatch(s) => throw new ValidationException("senha_invalida", "O PIN tem 4 a 8 dígitos."),
            SenhaTipo.Alfanumerica when s.Length > 64 => throw new ValidationException("senha_invalida", "Palavra-passe demasiado longa."),
            SenhaTipo.Padrao when !PadraoValido(s) => throw new ValidationException("senha_invalida", "O padrão liga 4 a 9 pontos diferentes (ex.: 1-5-9-6)."),
            _ => s,
        };
    }

    /// <summary>"1-5-9-6": pontos 1 a 9 (da esquerda para a direita, de cima para baixo), sem repetir, 4 a 9.</summary>
    public static bool PadraoValido(string s)
    {
        var p = s.Split('-');
        return p.Length is >= 4 and <= 9 && p.All(x => x.Length == 1 && x[0] is >= '1' and <= '9') && p.Distinct().Count() == p.Length;
    }

    private static string PrimeiroNome(string? nome) => string.IsNullOrWhiteSpace(nome) ? "Cliente" : nome.Trim().Split(' ')[0];

    private static string? Limpo(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Trim();
        return t.Length > max ? t[..max] : t;
    }

    [GeneratedRegex(@"^\d{4,8}$")]
    private static partial Regex PinRegex();
}
