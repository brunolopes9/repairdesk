using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Reparacoes;
using RepairDesk.Services.Trabalhos;

namespace RepairDesk.API.Controllers;

/// <summary>
/// Sprint 354 (Doc 83 Pillar 9): gestão interna dos pedidos de reparação que
/// chegaram pelo widget público. Staff converte em Reparacao ou rejeita.
/// </summary>
[ApiController]
[Route("api/repair-requests")]
[Authorize]
public sealed class RepairRequestsController : ControllerBase
{
    private readonly IRepairRequestRepository _repo;
    private readonly IClienteService _clientes;
    private readonly IReparacaoService _reparacoes;
    private readonly ITrabalhoService _trabalhos;
    private readonly IAuditLogger _audit;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUser _user;

    public RepairRequestsController(
        IRepairRequestRepository repo,
        IClienteService clientes,
        IReparacaoService reparacoes,
        ITrabalhoService trabalhos,
        IAuditLogger audit,
        ITenantContext tenant,
        ICurrentUser user)
    {
        _repo = repo;
        _clientes = clientes;
        _reparacoes = reparacoes;
        _trabalhos = trabalhos;
        _audit = audit;
        _tenant = tenant;
        _user = user;
    }

    public sealed record RequestDto(
        Guid Id, string Nome, string? Email, string? Telefone, string Equipamento,
        string Descricao, RepairRequestEstado Estado, Guid? ReparacaoId,
        string? MotivoRejeicao, DateTime CreatedAt,
        // Sprint 436 (Doc 91 follow-up Codex): triagem.
        string? NotasInternas, RepairRequestPrioridade Prioridade,
        // Sprint 437 (Doc 91 follow-up Codex): segundo caminho de conversão.
        Guid? TrabalhoId,
        // Sprint 438 (Doc 91 follow-up Codex): canal de entrada.
        RepairRequestOrigem Origem);

    public sealed record UpdateTriagemRequest(string? NotasInternas, RepairRequestPrioridade Prioridade);

    [HttpPut("{id:guid}/triagem")]
    public async Task<ActionResult<RequestDto>> AtualizarTriagem(Guid id, [FromBody] UpdateTriagemRequest body, CancellationToken ct)
    {
        var req = await _repo.FindByIdAsync(id, ct);
        if (req is null) return NotFound();

        var notas = string.IsNullOrWhiteSpace(body.NotasInternas) ? null : body.NotasInternas.Trim();
        if (notas is { Length: > 2000 })
            return BadRequest(new { code = "notas_too_long", message = "Notas até 2000 chars." });

        req.NotasInternas = notas;
        req.Prioridade = body.Prioridade;
        await _repo.SaveAsync(ct);
        return Ok(MapDto(req));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RequestDto>>> List([FromQuery] RepairRequestEstado? estado, CancellationToken ct)
    {
        var rows = await _repo.ListAsync(estado, ct);
        return Ok(rows.Select(MapDto).ToList());
    }

    [HttpGet("count-pendentes")]
    public async Task<ActionResult<int>> CountPendentes(CancellationToken ct) =>
        Ok(await _repo.CountPendentesAsync(ct));

    /// <summary>Converte o pedido numa reparação real (lookup-or-create cliente).</summary>
    [HttpPost("{id:guid}/converter")]
    public async Task<ActionResult<RequestDto>> Converter(Guid id, CancellationToken ct)
    {
        var req = await _repo.FindByIdAsync(id, ct);
        if (req is null) return NotFound();
        if (req.Estado != RepairRequestEstado.Pendente)
            return Conflict(new { code = "not_pendente", message = "Pedido já foi tratado." });

        // Lookup-or-create cliente por telefone/email.
        var lookup = await _clientes.LookupOrCreateAsync(
            new CreateClienteRequest(req.Nome, req.Telefone, req.Email, null, "Criado via widget de pedido online."), ct);

        var rep = await _reparacoes.CreateAsync(new CreateReparacaoRequest(
            ClienteId: lookup.Cliente.Id,
            Equipamento: req.Equipamento,
            Avaria: req.Descricao,
            Imei: null,
            OrcamentoCents: null,
            Notas: "Pedido submetido online pelo cliente.",
            EstadoInicial: RepairStatus.Recebido), ct);

        req.Estado = RepairRequestEstado.Convertido;
        req.ReparacaoId = rep.Id;
        await _repo.SaveAsync(ct);

        if (_tenant.TenantId is { } tid)
            await _audit.LogAsync(AuditAction.Create, "RepairRequest", req.Id, new { ConvertedTo = rep.Id }, tid, _user.UserId, ct);

        return Ok(MapDto(req));
    }

    /// <summary>
    /// Sprint 437 (Doc 91): converte em Trabalho (orçamento) em vez de Reparacao.
    /// Útil quando o cliente quer só uma estimativa antes de trazer o equipamento.
    /// </summary>
    [HttpPost("{id:guid}/converter-em-trabalho")]
    public async Task<ActionResult<RequestDto>> ConverterEmTrabalho(Guid id, CancellationToken ct)
    {
        var req = await _repo.FindByIdAsync(id, ct);
        if (req is null) return NotFound();
        if (req.Estado != RepairRequestEstado.Pendente)
            return Conflict(new { code = "not_pendente", message = "Pedido já foi tratado." });

        var lookup = await _clientes.LookupOrCreateAsync(
            new CreateClienteRequest(req.Nome, req.Telefone, req.Email, null, "Criado via widget de pedido online."), ct);

        var titulo = string.IsNullOrWhiteSpace(req.Equipamento)
            ? $"Orçamento — {req.Nome}"
            : $"{req.Equipamento} — {req.Nome}";

        var trabalho = await _trabalhos.CreateAsync(new CreateTrabalhoRequest(
            ClienteId: lookup.Cliente.Id,
            Titulo: titulo,
            Descricao: req.Descricao,
            Categoria: JobCategory.Outro,
            OrcamentoCents: null,
            Notas: "Pedido submetido online pelo cliente."), ct);

        req.Estado = RepairRequestEstado.Convertido;
        req.TrabalhoId = trabalho.Id;
        await _repo.SaveAsync(ct);

        if (_tenant.TenantId is { } tid)
            await _audit.LogAsync(AuditAction.Create, "RepairRequest", req.Id, new { ConvertedToTrabalho = trabalho.Id }, tid, _user.UserId, ct);

        return Ok(MapDto(req));
    }

    /// <summary>
    /// Sprint 439 (Doc 91 follow-up): cria um pedido manualmente — para leads que
    /// chegam por telefone, balcão, etc., e o staff quer ter na mesma inbox.
    /// Estado inicial = Pendente; staff pode depois converter ou rejeitar como
    /// qualquer outro pedido.
    /// </summary>
    public sealed record CreateManualRequest(
        string Nome,
        string? Telefone,
        string? Email,
        string Equipamento,
        string Descricao,
        RepairRequestOrigem Origem,
        RepairRequestPrioridade? Prioridade,
        string? NotasInternas);

    [HttpPost("manual")]
    public async Task<ActionResult<RequestDto>> CreateManual([FromBody] CreateManualRequest body, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
            return Forbid();

        var nome = body.Nome?.Trim() ?? "";
        var equipamento = body.Equipamento?.Trim() ?? "";
        var descricao = body.Descricao?.Trim() ?? "";
        var telefone = string.IsNullOrWhiteSpace(body.Telefone) ? null : body.Telefone.Trim();
        var email = string.IsNullOrWhiteSpace(body.Email) ? null : body.Email.Trim();
        var notas = string.IsNullOrWhiteSpace(body.NotasInternas) ? null : body.NotasInternas.Trim();

        if (nome.Length < 2 || nome.Length > 120)
            return BadRequest(new { code = "nome_invalido", message = "Nome 2-120 chars." });
        if (equipamento.Length < 2 || equipamento.Length > 120)
            return BadRequest(new { code = "equipamento_invalido", message = "Equipamento 2-120 chars." });
        if (descricao.Length < 5 || descricao.Length > 2000)
            return BadRequest(new { code = "descricao_invalida", message = "Descrição 5-2000 chars." });
        if (telefone is null && email is null)
            return BadRequest(new { code = "contacto_obrigatorio", message = "Pelo menos telefone ou email." });
        // Widget só pode vir do endpoint público — staff a registar manualmente
        // tem de indicar canal real (telefone, email, etc.).
        if (body.Origem == RepairRequestOrigem.Widget)
            return BadRequest(new { code = "origem_invalida", message = "Para pedidos manuais escolhe um canal (telefone, email, etc.)." });
        if (notas is { Length: > 2000 })
            return BadRequest(new { code = "notas_too_long", message = "Notas até 2000 chars." });

        var req = new Core.Entities.RepairRequest
        {
            TenantId = tenantId,
            Nome = nome,
            Telefone = telefone,
            Email = email,
            Equipamento = equipamento,
            Descricao = descricao,
            Estado = RepairRequestEstado.Pendente,
            Origem = body.Origem,
            Prioridade = body.Prioridade ?? RepairRequestPrioridade.Normal,
            NotasInternas = notas,
        };
        await _repo.AddAsync(req, ct);

        await _audit.LogAsync(AuditAction.Create, "RepairRequest", req.Id, new { Manual = true, Origem = body.Origem }, tenantId, _user.UserId, ct);

        return Ok(MapDto(req));
    }

    public sealed record RejeitarRequest(string? Motivo);

    [HttpPost("{id:guid}/rejeitar")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RequestDto>> Rejeitar(Guid id, [FromBody] RejeitarRequest? body, CancellationToken ct)
    {
        var req = await _repo.FindByIdAsync(id, ct);
        if (req is null) return NotFound();
        if (req.Estado != RepairRequestEstado.Pendente)
            return Conflict(new { code = "not_pendente" });

        req.Estado = RepairRequestEstado.Rejeitado;
        req.MotivoRejeicao = string.IsNullOrWhiteSpace(body?.Motivo) ? null : body!.Motivo.Trim();
        await _repo.SaveAsync(ct);
        return Ok(MapDto(req));
    }

    private static RequestDto MapDto(Core.Entities.RepairRequest r) =>
        new(r.Id, r.Nome, r.Email, r.Telefone, r.Equipamento, r.Descricao,
            r.Estado, r.ReparacaoId, r.MotivoRejeicao, r.CreatedAt,
            r.NotasInternas, r.Prioridade, r.TrabalhoId, r.Origem);
}
