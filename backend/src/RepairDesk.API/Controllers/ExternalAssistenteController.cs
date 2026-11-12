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
using RepairDesk.Services.Financas;
using RepairDesk.Services.InternalTasks;
using RepairDesk.Services.Rotina;
using RepairDesk.Services.Vendas;

namespace RepairDesk.API.Controllers;

public sealed record AssistenteTarefaRequest(string Titulo, string? Foco, DateTime? Prazo, string? Notas);
public sealed record AssistenteRotinaRequest(string? Item, Guid? ItemId, string? Estado, decimal? Valor, string? Motivo, DateOnly? Dia);
public sealed record AssistenteResultado(string Resumo, Guid? DesfazerId, object? Dados);
public sealed record AssistenteAgoraDto(RotinaDiaDto Rotina, IReadOnlyList<InternalTaskDto> Prioridades, IReadOnlyList<AppointmentDto> AgendaHoje);

/// <summary>
/// Sprint 637 (Doc 98 F8): API do assistente por voz/chat do dono (serviço lateral «mender-assistente»).
/// Chave com scope «assistente» — age em nome de quem a criou. Escritas de baixo risco executam já e devolvem um
/// <c>desfazerId</c> válido 5 minutos; tudo fica registado em AssistenteAcoes. Sem vendas, faturas nem apagar.
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

    public ExternalAssistenteController(IRotinaService rotina, IInternalTaskService tarefas, IAppointmentService agenda, IVendaService vendas,
        IFinancasService financas, ICurrentUser user, ITenantContext tenant, AppDbContext db)
    {
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
