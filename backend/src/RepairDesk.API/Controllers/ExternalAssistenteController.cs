using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Appointments;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Despesas;
using RepairDesk.Services.Documents;
using RepairDesk.Services.Financas;
using RepairDesk.Services.InternalTasks;
using RepairDesk.Services.Rotina;
using RepairDesk.Services.Vendas;

namespace RepairDesk.API.Controllers;

public sealed record AssistenteTarefaRequest(string Titulo, string? Foco, DateTime? Prazo, string? Notas);
public sealed record AssistenteRotinaRequest(string? Item, Guid? ItemId, string? Estado, decimal? Valor, string? Motivo, DateOnly? Dia);
public sealed record AssistenteResultado(string Resumo, Guid? DesfazerId, object? Dados);

/// <summary>
/// Sprint 653: venda pelo assistente. <c>Total</c> = o que o cliente paga (valor final, nunca IVA por cima).
/// <c>Fatura</c> = quanto vai na fatura (com IVA); o resto até ao total fica «por fora». Sem <c>Fatura</c> → tudo faturado.
/// <c>Tipo</c>: «reparacao» (omissão) | «produto» | «servico». <c>Estado</c>: «orcamento» | «pronta» (omissão) | «entregue».
/// </summary>
public sealed record AssistenteVendaRequest(
    string? Cliente, string? Telefone, string? Tipo, string? Equipamento, string? Avaria, string Descricao,
    decimal Total, decimal? Fatura, decimal? Custo, string? Estado, string? Notas);

/// <summary>Sprint 653: despesa pelo assistente. Categoria pelo nome do enum (ex.: «Software», «Combustivel»); omissão «Outras».</summary>
public sealed record AssistenteDespesaRequest(string Descricao, decimal Valor, string? Categoria, string? Fornecedor, DateTime? Data, string? Notas);

public sealed record AssistenteFaturaResumo(Guid Id, string? Fornecedor, string? Numero, DateTime? Data, decimal? Total, string? Leitura, string? Sugestao, DateTime Chegou);
public sealed record AssistenteAgoraDto(RotinaDiaDto Rotina, IReadOnlyList<InternalTaskDto> Prioridades, IReadOnlyList<AppointmentDto> AgendaHoje);

/// <summary>
/// Sprint 637 (Doc 98 F8): API do assistente por voz/chat do dono (serviço lateral «mender-assistente»).
/// Chave com scope «assistente» — age em nome de quem a criou. Escritas de baixo risco executam já e devolvem um
/// <c>desfazerId</c> válido 5 minutos; tudo fica registado em AssistenteAcoes. Sprint 653: também vendas (fatura + por fora), despesas, tarefas e faturas recebidas (só ler).
/// </summary>
[ApiController]
[Route("api/external/assistente")]
[Authorize(AuthenticationSchemes = ApiKeyAuthHandler.SchemeName)]
[ApiScope("assistente")]
public sealed class ExternalAssistenteController : ControllerBase
{
    public static readonly TimeSpan JanelaDesfazer = TimeSpan.FromMinutes(5);

    private readonly IRotinaService _rotina;
    private readonly IInternalTaskService _tarefas;
    private readonly IAppointmentService _agenda;
    private readonly IVendaService _vendas;
    private readonly IFinancasService _financas;
    private readonly ICurrentUser _user;
    private readonly ITenantContext _tenant;
    private readonly AppDbContext _db;
    private readonly IClienteService _clientes;
    private readonly IDespesaService _despesas;
    private readonly ISupplierInvoiceImportService _faturas;

    public ExternalAssistenteController(IRotinaService rotina, IInternalTaskService tarefas, IAppointmentService agenda, IVendaService vendas,
        IFinancasService financas, ICurrentUser user, ITenantContext tenant, AppDbContext db,
        IClienteService clientes, IDespesaService despesas, ISupplierInvoiceImportService faturas)
    {
        _clientes = clientes;
        _despesas = despesas;
        _faturas = faturas;
        _rotina = rotina;
        _tarefas = tarefas;
        _agenda = agenda;
        _vendas = vendas;
        _financas = financas;
        _user = user;
        _tenant = tenant;
        _db = db;
    }

    private Guid Dono => _user.UserId ?? throw new ForbiddenException("chave_sem_dono", "Esta chave não está ligada a um utilizador (cria-a de novo, com o scope «assistente»).");

    /// <summary>O que é suposto estar a fazer agora, o próximo, prioridades de hoje e marcações de hoje.</summary>
    [HttpGet("agora")]
    public async Task<ActionResult<AssistenteAgoraDto>> Agora(CancellationToken ct)
    {
        _ = Dono;
        var dia = await _rotina.DiaAsync(null, ct);
        var tarefas = (await _tarefas.ListAsync(InternalTaskStatus.Pendente, null, null, ct)).Where(t => t.Foco == TarefaFoco.Hoje).ToList();
        var inicio = RotinaCalendario.ParaUtc(DateTime.SpecifyKind(dia.Dia.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified));
        var agenda = await _agenda.ListAsync(inicio, inicio.AddDays(1), null, ct);
        return Ok(new AssistenteAgoraDto(dia, tarefas, agenda));
    }

    [HttpGet("rotina")]
    public async Task<ActionResult<RotinaDiaDto>> Rotina([FromQuery] DateOnly? dia, CancellationToken ct)
    {
        _ = Dono;
        return Ok(await _rotina.DiaAsync(dia, ct));
    }

    /// <summary>Cria tarefa ou ideia. foco: «hoje» | «semana» | «ideia» (omissão) | «nenhum».</summary>
    [HttpPost("tarefas")]
    public async Task<ActionResult<AssistenteResultado>> CriarTarefa([FromBody] AssistenteTarefaRequest req, CancellationToken ct)
    {
        var dono = Dono;
        var foco = (req.Foco ?? "ideia").Trim().ToLowerInvariant() switch
        {
            "hoje" => TarefaFoco.Hoje,
            "semana" => TarefaFoco.Semana,
            "nenhum" => TarefaFoco.Nenhum,
            _ => TarefaFoco.Ideia,
        };
        var t = await _tarefas.CreateAsync(new CreateInternalTaskRequest(req.Titulo, req.Notas, req.Prazo, dono, null, foco), ct);
        var acao = await Registar("tarefa", t.Id, null, null, $"Tarefa criada: {t.Title} ({foco})", ct);
        return Ok(new AssistenteResultado(acao.Resumo, acao.Id, t));
    }

    /// <summary>
    /// Marca um item da rotina de hoje (ou de <c>dia</c>) por título aproximado ou id. estado: «feito» (omissão) |
    /// «saltado». <c>valor</c> = peso em kg para o item de peso.
    /// </summary>
    [HttpPost("rotina")]
    public async Task<ActionResult<AssistenteResultado>> MarcarRotina([FromBody] AssistenteRotinaRequest req, CancellationToken ct)
    {
        _ = Dono;
        var dia = await _rotina.DiaAsync(req.Dia, ct);
        RotinaDiaItemDto? linha = req.ItemId is { } id
            ? dia.Itens.FirstOrDefault(l => l.Item.Id == id)
            : Encontrar(dia.Itens, req.Item);
        if (linha is null)
            throw new NotFoundException("RotinaItem", req.Item ?? req.ItemId?.ToString() ?? "?");
        var estado = (req.Estado ?? "feito").Trim().ToLowerInvariant().StartsWith("salt") ? RotinaRegistoEstado.Saltado : RotinaRegistoEstado.Feito;
        var antes = JsonSerializer.Serialize(new { estado = (int?)linha.Estado, valor = linha.Valor, motivo = linha.Motivo });
        var r = await _rotina.RegistarAsync(linha.Item.Id, dia.Dia, new RegistarRotinaRequest(estado, req.Motivo, req.Valor, null), ct);
        var acao = await Registar("rotina", linha.Item.Id, dia.Dia.ToString("yyyy-MM-dd"), antes,
            $"{linha.Item.Titulo}: {(estado == RotinaRegistoEstado.Feito ? "feito" : "saltado")}{(req.Valor is { } v ? $" ({v})" : "")}", ct);
        return Ok(new AssistenteResultado(acao.Resumo, acao.Id, r));
    }

    [HttpGet("agenda")]
    public async Task<ActionResult<IReadOnlyList<AppointmentDto>>> Agenda([FromQuery] int dias = 7, CancellationToken ct = default)
    {
        _ = Dono;
        var de = DateTime.UtcNow.Date;
        return Ok(await _agenda.ListAsync(de, de.AddDays(Math.Clamp(dias, 1, 31)), null, ct));
    }

    [HttpGet("pecas")]
    public async Task<ActionResult<IReadOnlyList<PecaEncomendaDto>>> Pecas(CancellationToken ct)
    {
        _ = Dono;
        return Ok(await _vendas.PecasEncomendaAsync(ct));
    }

    [HttpGet("financas")]
    public async Task<ActionResult<FinancasMesDto>> Financas([FromQuery] int? ano, [FromQuery] int? mes, CancellationToken ct)
    {
        _ = Dono;
        var agora = DateTime.UtcNow;
        return Ok(await _financas.MesAsync(ano ?? agora.Year, mes ?? agora.Month, ct));
    }

    /// <summary>Sprint 653: tarefas pendentes (todas, ou só um foco: «hoje» | «semana» | «ideia»).</summary>
    [HttpGet("tarefas")]
    public async Task<ActionResult<IReadOnlyList<InternalTaskDto>>> Tarefas([FromQuery] string? foco, CancellationToken ct)
    {
        _ = Dono;
        var lista = await _tarefas.ListAsync(InternalTaskStatus.Pendente, null, null, ct);
        TarefaFoco? f = (foco ?? "").Trim().ToLowerInvariant() switch
        {
            "hoje" => TarefaFoco.Hoje, "semana" => TarefaFoco.Semana, "ideia" => TarefaFoco.Ideia, _ => null,
        };
        return Ok(f is null ? lista : lista.Where(t => t.Foco == f).ToList());
    }

    /// <summary>
    /// Sprint 653: cria uma venda. Cliente por nome (aproximado) ou telefone; se não existir e vier telefone, cria-o.
    /// Preço = valor final ao cliente: linha faturada (com IVA 23 %) + linha «por fora» com o resto. Custo vai como
    /// «custo s/ fatura» da linha faturada. Desfazer = cancelar a venda.
    /// </summary>
    [HttpPost("vendas")]
    public async Task<ActionResult<AssistenteResultado>> CriarVenda([FromBody] AssistenteVendaRequest req, CancellationToken ct)
    {
        _ = Dono;
        if (string.IsNullOrWhiteSpace(req.Descricao)) throw new ValidationException("descricao_obrigatoria", "Diz o que foi feito ou vendido.");
        if (req.Total is <= 0 or > 100_000) throw new ValidationException("total_invalido", "O total tem de ser entre 0 e 100 000 €.");
        var fatura = req.Fatura ?? req.Total;
        if (fatura < 0 || fatura > req.Total) throw new ValidationException("fatura_invalida", "O valor da fatura tem de estar entre 0 e o total.");
        var tipo = (req.Tipo ?? "reparacao").Trim().ToLowerInvariant() switch
        {
            "produto" => VendaTipo.Produto, "servico" or "serviço" => VendaTipo.Servico, _ => VendaTipo.Reparacao,
        };

        var clienteId = await ResolverClienteAsync(req.Cliente, req.Telefone, ct);
        if (tipo == VendaTipo.Reparacao && clienteId is null)
            throw new ValidationException("cliente_obrigatorio", "Uma reparação precisa de cliente: diz o nome (e o telefone, se for novo).");

        static int C(decimal v) => (int)Math.Round(v * 100m, MidpointRounding.AwayFromZero);
        var linhas = new List<VendaLinhaWriteRequest>();
        if (fatura > 0)
            linhas.Add(new VendaLinhaWriteRequest(null, null, req.Descricao.Trim(), 1, C(fatura), IvaRate: 23m,
                CustoSemFaturaCents: req.Custo is { } c ? C(c) : null));
        var porFora = req.Total - fatura;
        if (porFora > 0)
            linhas.Add(new VendaLinhaWriteRequest(null, null, fatura > 0 ? "Por fora" : req.Descricao.Trim(), 1, C(porFora), IvaRate: 0m, PorFora: true,
                CustoSemFaturaCents: fatura > 0 || req.Custo is null ? null : C(req.Custo.Value)));

        var estado = (req.Estado ?? "pronta").Trim().ToLowerInvariant();
        var notas = string.Join(" ", new[] { req.Notas?.Trim(), "Criada pelo assistente (Telegram)." }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var venda = await _vendas.CreateAsync(new VendaWriteRequest(tipo, clienteId, req.Equipamento, req.Avaria, notas, linhas,
            estado.StartsWith("entreg") ? VendaEstado.Entregue : VendaEstado.Orcamento, PaymentMethod.Outro), ct);
        if (estado.StartsWith("pront"))
            venda = await _vendas.MudarEstadoAsync(venda.Id, new MudarEstadoVendaRequest(VendaEstado.Pronta), ct);

        var resumo = $"Venda #{venda.Numero} criada ({venda.Estado}): {req.Descricao.Trim()} — {req.Total:0.00} €"
                     + (porFora > 0 ? $" ({fatura:0.00} na fatura + {porFora:0.00} por fora)" : " (tudo na fatura)")
                     + (req.Custo is { } cu ? $", custo {cu:0.00} €" : "");
        var acao = await Registar("venda", venda.Id, null, null, resumo, ct);
        return Ok(new AssistenteResultado(acao.Resumo, acao.Id, new { venda.Id, venda.Numero, venda.Estado, venda.TotalCents, venda.Lucro }));
    }

    /// <summary>Sprint 653: faturas de fornecedor que chegaram e estão por tratar (compra ou despesa — decide o Bruno).</summary>
    [HttpGet("faturas")]
    public async Task<ActionResult<IReadOnlyList<AssistenteFaturaResumo>>> Faturas(CancellationToken ct)
    {
        _ = Dono;
        var lista = await _faturas.ListPendingAsync(50, ct);
        return Ok(lista.Select(f => new AssistenteFaturaResumo(f.Id, f.FornecedorName, f.DocumentNumber, f.DocumentDate,
            f.TotalCents / 100m, f.ParseConfidence, f.FornecedorDefaultAction, f.CreatedAt)).ToList());
    }

    /// <summary>Sprint 653: regista uma despesa (âmbito negócio por omissão). Desfazer = apagar.</summary>
    [HttpPost("despesas")]
    public async Task<ActionResult<AssistenteResultado>> CriarDespesa([FromBody] AssistenteDespesaRequest req, CancellationToken ct)
    {
        _ = Dono;
        if (string.IsNullOrWhiteSpace(req.Descricao)) throw new ValidationException("descricao_obrigatoria", "Diz o que foi a despesa.");
        if (req.Valor is <= 0 or > 1_000_000) throw new ValidationException("valor_invalido", "Valor da despesa inválido.");
        var categoria = Enum.TryParse<DespesaCategoria>((req.Categoria ?? "").Replace(" ", ""), true, out var cat) ? cat : DespesaCategoria.Outras;
        var d = await _despesas.CreateAsync(new CreateDespesaRequest(req.Descricao.Trim(), categoria,
            (int)Math.Round(req.Valor * 100m, MidpointRounding.AwayFromZero), req.Data ?? DateTime.UtcNow, req.Fornecedor, null,
            string.Join(" ", new[] { req.Notas?.Trim(), "Registada pelo assistente (Telegram)." }.Where(s => !string.IsNullOrWhiteSpace(s)))), ct);
        var acao = await Registar("despesa", d.Id, null, null, $"Despesa #{d.Numero}: {d.Descricao} — {req.Valor:0.00} € ({categoria})", ct);
        return Ok(new AssistenteResultado(acao.Resumo, acao.Id, d));
    }

    /// <summary>Cliente por telefone (exato) ou nome (aproximado). Vários com o mesmo nome → pede o telefone. Novo só com telefone.</summary>
    private async Task<Guid?> ResolverClienteAsync(string? nome, string? telefone, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(nome) && string.IsNullOrWhiteSpace(telefone)) return null;
        var termo = !string.IsNullOrWhiteSpace(telefone) ? telefone!.Trim() : nome!.Trim();
        var encontrados = (await _clientes.SearchAsync(termo, 1, 10, null, ct)).Items;
        if (!string.IsNullOrWhiteSpace(nome) && !string.IsNullOrWhiteSpace(telefone) && encontrados.Count == 0)
            encontrados = (await _clientes.SearchAsync(nome!.Trim(), 1, 10, null, ct)).Items;
        if (encontrados.Count == 1) return encontrados[0].Id;
        if (encontrados.Count > 1)
        {
            var exato = encontrados.Where(c => string.Equals(c.Nome.Trim(), nome?.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            if (exato.Count == 1) return exato[0].Id;
            throw new ConflictException("cliente_ambiguo",
                $"Há {encontrados.Count} clientes parecidos ({string.Join(", ", encontrados.Take(5).Select(c => $"{c.Nome} {c.Telefone}"))}). Diz o telefone.");
        }
        if (string.IsNullOrWhiteSpace(telefone) || string.IsNullOrWhiteSpace(nome))
            throw new NotFoundException("Cliente", $"{nome ?? telefone} — não existe; diz o nome e o telefone para o criar.");
        return (await _clientes.CreateAsync(new CreateClienteRequest(nome!.Trim(), telefone!.Trim(), null, null, "Criado pelo assistente (Telegram)."), ct)).Id;
    }

    /// <summary>Desfaz uma ação do assistente (só nos 5 minutos seguintes).</summary>
    [HttpPost("desfazer/{id:guid}")]
    public async Task<ActionResult<AssistenteResultado>> Desfazer(Guid id, CancellationToken ct)
    {
        var dono = Dono;
        var acao = await _db.AssistenteAcoes.FirstOrDefaultAsync(a => a.Id == id && a.UserId == dono, ct)
                   ?? throw new NotFoundException("AssistenteAcao", id);
        if (acao.DesfeitaEm is not null) return Ok(new AssistenteResultado("Já estava desfeito.", null, null));
        if (DateTime.UtcNow - acao.CreatedAt > JanelaDesfazer)
            throw new ConflictException("prazo_desfazer", "Já passaram mais de 5 minutos — desfaz no Mender.");
        if (acao.Tipo == "tarefa")
            await _tarefas.DeleteAsync(acao.EntidadeId, ct);
        else if (acao.Tipo == "venda")
            await _vendas.MudarEstadoAsync(acao.EntidadeId, new MudarEstadoVendaRequest(VendaEstado.Cancelada), ct);
        else if (acao.Tipo == "despesa")
            await _despesas.DeleteAsync(acao.EntidadeId, ct);
        else if (acao.Tipo == "rotina" && DateOnly.TryParse(acao.Dia, out var dia))
        {
            var antes = JsonSerializer.Deserialize<JsonElement>(acao.Antes ?? "{}");
            RotinaRegistoEstado? estado = antes.TryGetProperty("estado", out var e) && e.ValueKind == JsonValueKind.Number ? (RotinaRegistoEstado)e.GetInt32() : null;
            decimal? valor = antes.TryGetProperty("valor", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : null;
            string? motivo = antes.TryGetProperty("motivo", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
            await _rotina.RegistarAsync(acao.EntidadeId, dia, new RegistarRotinaRequest(estado == RotinaRegistoEstado.Adiado ? null : estado, motivo, valor, null), ct);
        }
        acao.DesfeitaEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(new AssistenteResultado($"Desfeito: {acao.Resumo}", null, null));
    }

    private async Task<AssistenteAcao> Registar(string tipo, Guid entidade, string? dia, string? antes, string resumo, CancellationToken ct)
    {
        var a = new AssistenteAcao
        {
            TenantId = _tenant.TenantId ?? Guid.Empty, UserId = Dono, Tipo = tipo, EntidadeId = entidade, Dia = dia, Antes = antes,
            Resumo = resumo.Length > 300 ? resumo[..300] : resumo,
        };
        _db.AssistenteAcoes.Add(a);
        await _db.SaveChangesAsync(ct);
        return a;
    }

    /// <summary>Título aproximado, sem acentos: «almoco» encontra «Almoço»; prefere o que ainda não está feito.</summary>
    internal static RotinaDiaItemDto? Encontrar(IReadOnlyList<RotinaDiaItemDto> itens, string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var q = FinancasExcelImporter.Norm(texto);
        var candidatos = itens.Where(l => FinancasExcelImporter.Norm(l.Item.Titulo).Contains(q) || q.Contains(FinancasExcelImporter.Norm(l.Item.Titulo))).ToList();
        if (candidatos.Count == 0)
        {
            var palavras = q.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(p => p.Length > 2).ToList();
            candidatos = itens.Where(l => palavras.Any(p => FinancasExcelImporter.Norm(l.Item.Titulo).Contains(p))).ToList();
        }
        return candidatos.OrderBy(l => l.Estado == RotinaRegistoEstado.Feito ? 1 : 0)
            .ThenBy(l => Math.Abs((l.InicioUtc - DateTime.UtcNow).TotalMinutes)).FirstOrDefault();
    }
}
