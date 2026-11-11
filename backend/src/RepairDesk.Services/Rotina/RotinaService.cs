using System.Globalization;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Rotina;

public sealed record RotinaItemDto(
    Guid Id, string Titulo, string? Detalhe, RotinaTipo Tipo, string Hora, int DuracaoMin, int Dias,
    int? IntervaloDias, DateOnly? DataInicio, string? AvisosAntes, bool Insistir, bool AvisarFim, bool Ativo);

public sealed record GuardarRotinaItemRequest(
    string Titulo, string? Detalhe, RotinaTipo Tipo, string Hora, int DuracaoMin, int Dias,
    int? IntervaloDias, DateOnly? DataInicio, string? AvisosAntes, bool Insistir, bool AvisarFim, bool Ativo = true);

public sealed record RotinaDiaItemDto(
    RotinaItemDto Item, DateTime InicioUtc, DateTime FimUtc, RotinaRegistoEstado? Estado,
    DateTime? AdiadoAte, string? Motivo, decimal? Valor, bool Atrasado);

public sealed record RotinaDiaDto(
    DateOnly Dia, DateTime AgoraUtc, Guid? AtualId, Guid? ProximoId, IReadOnlyList<RotinaDiaItemDto> Itens,
    int Feitos, int Devidos, int MeditacoesFeitas, int MeditacoesTotal);

/// <summary>Estado null = desfazer o registo do dia.</summary>
public sealed record RegistarRotinaRequest(RotinaRegistoEstado? Estado, string? Motivo, decimal? Valor, int? AdiarMin);

public sealed record RotinaValorDto(DateOnly Dia, decimal Valor);

public interface IRotinaService
{
    Task<IReadOnlyList<RotinaItemDto>> ItensAsync(CancellationToken ct = default);
    Task<RotinaItemDto> CriarAsync(GuardarRotinaItemRequest req, CancellationToken ct = default);
    Task<RotinaItemDto> AtualizarAsync(Guid id, GuardarRotinaItemRequest req, CancellationToken ct = default);
    Task ApagarAsync(Guid id, CancellationToken ct = default);
    Task<RotinaDiaDto> DiaAsync(DateOnly? dia, CancellationToken ct = default);
    Task<RotinaDiaItemDto> RegistarAsync(Guid itemId, DateOnly dia, RegistarRotinaRequest req, CancellationToken ct = default);
    Task<IReadOnlyList<RotinaValorDto>> ValoresAsync(Guid itemId, CancellationToken ct = default);
}

/// <summary>
/// Sprint 615 (Doc 98): a rotina pessoal do utilizador (blocos, hábitos e limites) e o registo do dia.
/// Cada utilizador só mexe na sua. Os avisos no telemóvel estão em <see cref="RotinaAvisosService"/>.
/// </summary>
public sealed class RotinaService : IRotinaService
{
    public const int MaxItens = 80;
    private const int DiasParaTras = 7;

    private readonly IRotinaRepository _repo;
    private readonly ICurrentUser _user;
    private readonly ITenantContext _tenant;

    public RotinaService(IRotinaRepository repo, ICurrentUser user, ITenantContext tenant)
    {
        _repo = repo;
        _user = user;
        _tenant = tenant;
    }

    private Guid UserId => _user.UserId ?? throw new ForbiddenException("user_required", "Sem utilizador autenticado.");
    private static DateTime Agora => DateTime.UtcNow;

    public async Task<IReadOnlyList<RotinaItemDto>> ItensAsync(CancellationToken ct = default)
        => (await _repo.ItensDoUtilizadorAsync(UserId, ct)).Select(Dto).ToList();

    public async Task<RotinaItemDto> CriarAsync(GuardarRotinaItemRequest req, CancellationToken ct = default)
    {
        var userId = UserId;
        if ((await _repo.ItensDoUtilizadorAsync(userId, ct)).Count >= MaxItens)
            throw new ValidationException("demasiados_itens", $"A rotina tem no máximo {MaxItens} itens.");
        var item = new RotinaItem { Titulo = "", UserId = userId, TenantId = _tenant.TenantId ?? Guid.Empty };
        Aplicar(item, req);
        await _repo.AddItemAsync(item, ct);
        await _repo.SaveAsync(ct);
        return Dto(item);
    }

    public async Task<RotinaItemDto> AtualizarAsync(Guid id, GuardarRotinaItemRequest req, CancellationToken ct = default)
    {
        var item = await MeuItemAsync(id, ct);
        Aplicar(item, req);
        item.UpdatedAt = Agora;
        await _repo.SaveAsync(ct);
        return Dto(item);
    }

    public async Task ApagarAsync(Guid id, CancellationToken ct = default)
    {
        var item = await MeuItemAsync(id, ct);
        item.IsDeleted = true;
        item.Ativo = false;
        await _repo.SaveAsync(ct);
    }

    public async Task<RotinaDiaDto> DiaAsync(DateOnly? dia, CancellationToken ct = default)
    {
        var agora = Agora;
        var d = dia ?? RotinaCalendario.HojeLisboa(agora);
        var itens = (await _repo.ItensDoUtilizadorAsync(UserId, ct))
            .Where(i => i.Ativo && RotinaCalendario.Acontece(i, d)).ToList();
        var registos = (await _repo.RegistosAsync(UserId, d, d, ct)).ToDictionary(r => r.RotinaItemId);

        var linhas = itens
            .Select(i => Linha(i, d, registos.GetValueOrDefault(i.Id), agora))
            .OrderBy(l => l.InicioUtc).ThenBy(l => l.Item.Titulo).ToList();

        static bool Fechado(RotinaDiaItemDto l) => l.Estado is RotinaRegistoEstado.Feito or RotinaRegistoEstado.Saltado;
        var atual = linhas.Where(l => l.Item.DuracaoMin > 0 && l.InicioUtc <= agora && agora < l.FimUtc && !Fechado(l))
            .OrderByDescending(l => l.InicioUtc).FirstOrDefault();
        var proximo = linhas.FirstOrDefault(l => l.InicioUtc > agora && !Fechado(l));
        var devidos = linhas.Count(l => l.InicioUtc <= agora);
        var feitos = linhas.Count(l => l.Estado == RotinaRegistoEstado.Feito);
        var med = linhas.Where(l => l.Item.Tipo == RotinaTipo.Meditacao).ToList();

        return new RotinaDiaDto(d, agora, atual?.Item.Id, proximo?.Item.Id, linhas, feitos, devidos,
            med.Count(l => l.Estado == RotinaRegistoEstado.Feito), med.Count);
    }

    public async Task<RotinaDiaItemDto> RegistarAsync(Guid itemId, DateOnly dia, RegistarRotinaRequest req, CancellationToken ct = default)
    {
        var item = await MeuItemAsync(itemId, ct);
        var agora = Agora;
        var hoje = RotinaCalendario.HojeLisboa(agora);
        if (dia > hoje || dia < hoje.AddDays(-DiasParaTras))
            throw new ValidationException("dia_invalido", $"Só podes registar hoje ou até {DiasParaTras} dias para trás.");
        if (!RotinaCalendario.Acontece(item, dia))
            throw new ValidationException("nao_acontece", "Este item não está na rotina desse dia.");

        var motivo = string.IsNullOrWhiteSpace(req.Motivo) ? null : req.Motivo.Trim();
        if (motivo is { Length: > 200 }) throw new ValidationException("motivo_longo", "O motivo tem no máximo 200 caracteres.");

        var reg = await _repo.FindRegistoAsync(item.Id, dia, ct);
        var novo = reg is null;
        reg ??= new RotinaRegisto { TenantId = item.TenantId, UserId = item.UserId, RotinaItemId = item.Id, Dia = dia };

        switch (req.Estado)
        {
            case null:
                reg.Estado = null;
                reg.RegistadoEm = null;
                reg.AdiadoAte = null;
                reg.Motivo = null;
                reg.Valor = null;
                break;
            case RotinaRegistoEstado.Feito:
                if (item.Tipo == RotinaTipo.Peso)
                {
                    if (req.Valor is not { } kg || kg < 20 || kg > 400)
                        throw new ValidationException("valor_invalido", "Indica o peso em kg (20 a 400).");
                    reg.Valor = Math.Round(kg, 2);
                }
                else reg.Valor = req.Valor is { } v ? Math.Round(v, 2) : null;
                reg.Estado = RotinaRegistoEstado.Feito;
                reg.RegistadoEm = agora;
                reg.AdiadoAte = null;
                reg.Motivo = motivo;
                break;
            case RotinaRegistoEstado.Saltado:
                reg.Estado = RotinaRegistoEstado.Saltado;
                reg.RegistadoEm = agora;
                reg.AdiadoAte = null;
                reg.Motivo = motivo;
                break;
            case RotinaRegistoEstado.Adiado:
                if (dia != hoje) throw new ValidationException("adiar_so_hoje", "Só se adia o que é de hoje.");
                if (item.Tipo == RotinaTipo.Limite && (motivo is null || motivo.Length < 3))
                    throw new ValidationException("motivo_obrigatorio", "Para passar do limite tens de dizer porquê.");
                var min = req.AdiarMin ?? 15;
                if (min is < 5 or > 120) throw new ValidationException("adiar_invalido", "Adia entre 5 e 120 minutos.");
                var inicio = RotinaCalendario.InicioUtc(item, dia);
                reg.Estado = RotinaRegistoEstado.Adiado;
                reg.RegistadoEm = agora;
                reg.AdiadoAte = (agora > inicio ? agora : inicio).AddMinutes(min);
                reg.Motivo = motivo;
                break;
            default:
                throw new ValidationException("estado_invalido", "Estado inválido.");
        }
        if (novo) await _repo.AddRegistoAsync(reg, ct);
        await _repo.SaveAsync(ct);
        return Linha(item, dia, reg, agora);
    }

    public async Task<IReadOnlyList<RotinaValorDto>> ValoresAsync(Guid itemId, CancellationToken ct = default)
    {
        var item = await MeuItemAsync(itemId, ct);
        return (await _repo.ValoresAsync(item.UserId, item.Id, 200, ct))
            .OrderBy(r => r.Dia).Select(r => new RotinaValorDto(r.Dia, r.Valor!.Value)).ToList();
    }

    private async Task<RotinaItem> MeuItemAsync(Guid id, CancellationToken ct)
    {
        var item = await _repo.FindItemAsync(id, ct);
        if (item is null || item.UserId != UserId) throw new NotFoundException("RotinaItem", id);
        return item;
    }

    private static RotinaDiaItemDto Linha(RotinaItem i, DateOnly d, RotinaRegisto? r, DateTime agora)
    {
        var inicio = RotinaCalendario.InicioUtc(i, d);
        var atrasado = (r?.Estado is null && inicio.AddMinutes(15) <= agora)
            || (r?.Estado == RotinaRegistoEstado.Adiado && r.AdiadoAte <= agora);
        return new RotinaDiaItemDto(Dto(i), inicio, RotinaCalendario.FimUtc(i, d), r?.Estado, r?.AdiadoAte, r?.Motivo, r?.Valor, atrasado);
    }

    private static void Aplicar(RotinaItem item, GuardarRotinaItemRequest req)
    {
        var titulo = req.Titulo?.Trim() ?? string.Empty;
        if (titulo.Length is 0 or > 80) throw new ValidationException("titulo_invalido", "O título tem de 1 a 80 caracteres.");
        var detalhe = string.IsNullOrWhiteSpace(req.Detalhe) ? null : req.Detalhe.Trim();
        if (detalhe is { Length: > 1000 }) throw new ValidationException("detalhe_longo", "O detalhe tem no máximo 1000 caracteres.");
        if (!Enum.IsDefined(req.Tipo)) throw new ValidationException("tipo_invalido", "Tipo inválido.");
        if (!TimeOnly.TryParseExact(req.Hora?.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var hora))
            throw new ValidationException("hora_invalida", "Hora no formato HH:mm.");
        if (req.DuracaoMin is < 0 or > 720) throw new ValidationException("duracao_invalida", "Duração de 0 a 720 minutos.");
        if (req.IntervaloDias is { } n && n is < 1 or > 90)
            throw new ValidationException("intervalo_invalido", "Repetir de 1 a 90 dias.");
        if (req.IntervaloDias is null && (req.Dias <= 0 || req.Dias > RotinaCalendario.TodosOsDias))
            throw new ValidationException("dias_invalidos", "Escolhe pelo menos um dia da semana.");

        item.Titulo = titulo;
        item.Detalhe = detalhe;
        item.Tipo = req.Tipo;
        item.Hora = hora;
        item.DuracaoMin = req.DuracaoMin;
        item.IntervaloDias = req.IntervaloDias;
        item.Dias = req.IntervaloDias is null ? req.Dias : 0;
        item.DataInicio = req.IntervaloDias is null ? null : req.DataInicio ?? item.DataInicio ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var antes = RotinaCalendario.MinutosAntes(req.AvisosAntes);
        item.AvisosAntes = antes.Count == 0 ? null : string.Join(',', antes);
        item.Insistir = req.Insistir;
        item.AvisarFim = req.AvisarFim && req.DuracaoMin > 0;
        item.Ativo = req.Ativo;
    }

    public static RotinaItemDto Dto(RotinaItem i) => new(
        i.Id, i.Titulo, i.Detalhe, i.Tipo, i.Hora.ToString("HH:mm", CultureInfo.InvariantCulture), i.DuracaoMin, i.Dias,
        i.IntervaloDias, i.DataInicio, i.AvisosAntes, i.Insistir, i.AvisarFim, i.Ativo);
}
