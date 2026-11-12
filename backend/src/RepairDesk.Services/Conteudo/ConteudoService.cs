using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Push;

namespace RepairDesk.Services.Conteudo;

public interface IConteudoService
{
    Task<IReadOnlyList<ConteudoPublicacaoDto>> ListAsync(IReadOnlyCollection<ConteudoEstado>? estados, DateTime? deUtc, DateTime? ateUtc, CancellationToken ct = default);
    Task<ConteudoPublicacaoDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<ConteudoResumoDto> ResumoAsync(CancellationToken ct = default);

    Task<ConteudoPublicacaoDto> CreateAsync(GuardarPublicacaoRequest req, CancellationToken ct = default);
    Task<ConteudoPublicacaoDto> UpdateAsync(Guid id, GuardarPublicacaoRequest req, CancellationToken ct = default);
    Task<ConteudoPublicacaoDto> SubmeterAsync(Guid id, CancellationToken ct = default);
    /// <summary>Aprova; com <paramref name="hora"/> aprova já para essa hora (ou «agora»).</summary>
    Task<ConteudoPublicacaoDto> AprovarAsync(Guid id, HoraPublicacaoRequest? hora = null, CancellationToken ct = default);
    /// <summary>Muda a hora de uma publicação por aprovar ou aprovada, sem a devolver à fila.</summary>
    Task<ConteudoPublicacaoDto> MudarHoraAsync(Guid id, HoraPublicacaoRequest req, CancellationToken ct = default);
    Task<ConteudoPublicacaoDto> RejeitarAsync(Guid id, string? motivo, CancellationToken ct = default);
    Task<ConteudoPublicacaoDto> CancelarAsync(Guid id, CancellationToken ct = default);
    /// <summary>Sprint 629: marca uma publicação que já saiu como apagada nas redes (deixa de contar).</summary>
    Task<ConteudoPublicacaoDto> MarcarRemovidaAsync(Guid id, CancellationToken ct = default);
    Task<ConteudoPublicacaoDto> AtualizarDestinoAsync(Guid id, RedeSocial rede, AtualizarDestinoRequest req, CancellationToken ct = default);
    Task<ConteudoPublicacaoDto> GuardarOpcoesTikTokAsync(Guid id, GuardarOpcoesTikTokRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ConteudoFicheiroDto>> BibliotecaAsync(bool soPorProcessar, CancellationToken ct = default);
    Task<UploadTicketDto> PedirUploadAsync(PedirUploadRequest req, CancellationToken ct = default);
    Task<ConteudoFicheiroDto> ConfirmarUploadAsync(Guid ficheiroId, ConfirmarUploadRequest req, CancellationToken ct = default);
    Task<ConteudoFicheiroDto> MarcarProcessadoAsync(Guid ficheiroId, CancellationToken ct = default);
    Task DeleteFicheiroAsync(Guid ficheiroId, CancellationToken ct = default);

    /// <summary>Proxy de binários quando não há URLs pré-assinados (dev local).</summary>
    Task<(Stream Conteudo, string ContentType, string Nome)> AbrirBinarioAsync(Guid ficheiroId, CancellationToken ct = default);
    Task GravarBinarioAsync(Guid ficheiroId, Stream conteudo, string contentType, CancellationToken ct = default);
}

/// <summary>
/// Sprint 598 (Doc 95): calendário editorial das redes sociais.
///
/// Regras principais:
/// - Só o dono aprova (a restrição de role fica no controller); a API key do Claude cria,
///   submete e regista publicações, mas nunca aprova.
/// - Editar uma publicação aprovada devolve-a a «Por aprovar» — o dono aprovou outra versão.
/// - <see cref="ConteudoPublicacao.AutoAprovada"/> só vale para a foto promo de um artigo
///   (autorização permanente do dono), nunca para outros formatos.
/// - Cada rede tem estado próprio; a publicação fica «Publicada» quando todas saíram.
/// </summary>
public sealed class ConteudoService : IConteudoService
{
    public const long MaxBytes = 5L * 1024 * 1024 * 1024; // limite de um PUT simples no R2
    public const int MaxNotas = 8000; // cabe um guião completo de reel (Sprint 609)
    private static readonly TimeSpan ValidadeUpload = TimeSpan.FromHours(2);
    private static readonly TimeSpan ValidadeLeitura = TimeSpan.FromHours(6);

    private static readonly Dictionary<string, (ConteudoFicheiroTipo Tipo, string Ext)> TiposPermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = (ConteudoFicheiroTipo.Imagem, ".jpg"),
        ["image/png"] = (ConteudoFicheiroTipo.Imagem, ".png"),
        ["image/webp"] = (ConteudoFicheiroTipo.Imagem, ".webp"),
        // Fotos do iPhone: o material bruto vem em HEIC; serve para a biblioteca mas não sai nas redes.
        ["image/heic"] = (ConteudoFicheiroTipo.Imagem, ".heic"),
        ["image/heif"] = (ConteudoFicheiroTipo.Imagem, ".heif"),
        ["video/mp4"] = (ConteudoFicheiroTipo.Video, ".mp4"),
        ["video/quicktime"] = (ConteudoFicheiroTipo.Video, ".mov"),
        ["video/x-m4v"] = (ConteudoFicheiroTipo.Video, ".m4v"),
    };

    /// <summary>Formatos que as redes aceitam numa publicação (HEIC fica só como material bruto).</summary>
    private static readonly HashSet<string> TiposPublicaveis = new(StringComparer.OrdinalIgnoreCase)
        { "image/jpeg", "image/png", "image/webp", "video/mp4", "video/quicktime" };

    private static readonly ConteudoEstado[] EstadosEditaveis =
        { ConteudoEstado.Rascunho, ConteudoEstado.PorAprovar, ConteudoEstado.Aprovada, ConteudoEstado.Rejeitada };

    private readonly IConteudoRepository _repo;
    private readonly IPhotoStorage _storage;
    private readonly IDirectAccessStorage? _direct;
    private readonly IConteudoLinkSigner _signer;
    private readonly IStaffPushQueue _push;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUser _user;
    private readonly IAuditLogger _audit;

    public ConteudoService(
        IConteudoRepository repo,
        IPhotoStorage storage,
        IEnumerable<IDirectAccessStorage> direct,
        IConteudoLinkSigner signer,
        IStaffPushQueue push,
        ITenantContext tenant,
        ICurrentUser user,
        IAuditLogger audit)
    {
        _repo = repo;
        _storage = storage;
        _direct = direct.FirstOrDefault();
        _signer = signer;
        _push = push;
        _tenant = tenant;
        _user = user;
        _audit = audit;
    }

    private Guid TenantId => _tenant.TenantId ?? throw new ForbiddenException("tenant_required", "Sem tenant.");

    // ---- Leitura ------------------------------------------------------------------------------

    public async Task<IReadOnlyList<ConteudoPublicacaoDto>> ListAsync(
        IReadOnlyCollection<ConteudoEstado>? estados, DateTime? deUtc, DateTime? ateUtc, CancellationToken ct = default)
    {
        var list = await _repo.ListPublicacoesAsync(estados, ToUtc(deUtc), ToUtc(ateUtc), ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<ConteudoPublicacaoDto> GetAsync(Guid id, CancellationToken ct = default)
        => ToDto(await Load(id, ct));

    public async Task<ConteudoResumoDto> ResumoAsync(CancellationToken ct = default)
    {
        var porAprovar = await _repo.CountPorAprovarAsync(ct);
        var biblioteca = await _repo.ListBibliotecaAsync(soPorProcessar: true, ct);
        return new ConteudoResumoDto(porAprovar, biblioteca.Count);
    }

    // ---- Publicações --------------------------------------------------------------------------

    public async Task<ConteudoPublicacaoDto> CreateAsync(GuardarPublicacaoRequest req, CancellationToken ct = default)
    {
        var p = new ConteudoPublicacao
        {
            TenantId = TenantId,
            Titulo = "",
            Origem = _user.ServiceApiKeyId is not null ? ConteudoOrigem.Claude : ConteudoOrigem.Manual,
        };
        await Aplicar(p, req, ct);
        await _repo.AddAsync(p, ct);

        if (req.AutoAprovada)
        {
            ValidarParaAprovacao(p);
            p.AutoAprovada = true;
            p.Estado = ConteudoEstado.Aprovada;
            p.AprovadoEm = DateTime.UtcNow;
        }
        else if (req.Submeter)
        {
            ValidarParaAprovacao(p);
            p.Estado = ConteudoEstado.PorAprovar;
        }

        await _repo.SaveAsync(ct);
        await Auditar(AuditAction.Create, p, new { p.Titulo, p.Formato, estado = p.Estado.ToString() }, ct);
        if (p.Estado == ConteudoEstado.PorAprovar) await AvisarPorAprovar(ct);
        return ToDto(p);
    }

    public async Task<ConteudoPublicacaoDto> UpdateAsync(Guid id, GuardarPublicacaoRequest req, CancellationToken ct = default)
    {
        var p = await Load(id, ct);
        if (!EstadosEditaveis.Contains(p.Estado))
            throw new ConflictException("conteudo_nao_editavel", $"Uma publicação «{Nome(p.Estado)}» já não pode ser editada.");
        if (req.AutoAprovada && !p.AutoAprovada)
            throw new ValidationException("auto_aprovacao_so_na_criacao", "A aprovação automática só se define ao criar a promo do dia.");

        var estavaAprovada = p.Estado == ConteudoEstado.Aprovada;
        await Aplicar(p, req, ct);

        if (p.AutoAprovada)
        {
            ValidarParaAprovacao(p); // continua dentro da autorização permanente
        }
        else if (estavaAprovada || req.Submeter)
        {
            // Aprovada + editada = o dono tem de ver a versão nova.
            ValidarParaAprovacao(p);
            p.Estado = ConteudoEstado.PorAprovar;
            p.AprovadoEm = null;
            p.AprovadoPorUserId = null;
            p.MotivoRejeicao = null;
        }
        else if (p.Estado == ConteudoEstado.PorAprovar)
        {
            ValidarParaAprovacao(p);
        }

        await _repo.SaveAsync(ct);
        await Auditar(AuditAction.Update, p, new { p.Titulo, estado = p.Estado.ToString() }, ct);
        if (p.Estado == ConteudoEstado.PorAprovar && (estavaAprovada || req.Submeter)) await AvisarPorAprovar(ct);
        return ToDto(p);
    }

    public async Task<ConteudoPublicacaoDto> SubmeterAsync(Guid id, CancellationToken ct = default)
    {
        var p = await Load(id, ct);
        if (p.Estado is not (ConteudoEstado.Rascunho or ConteudoEstado.Rejeitada))
            throw new ConflictException("conteudo_estado_invalido", $"Só rascunhos ou rejeitadas podem ser enviadas para aprovação (está «{Nome(p.Estado)}»).");
        ValidarParaAprovacao(p);
        p.Estado = ConteudoEstado.PorAprovar;
        p.MotivoRejeicao = null;
        await _repo.SaveAsync(ct);
        await Auditar(AuditAction.Update, p, new { estado = p.Estado.ToString() }, ct);
        await AvisarPorAprovar(ct);
        return ToDto(p);
    }

    public async Task<ConteudoPublicacaoDto> AprovarAsync(Guid id, HoraPublicacaoRequest? hora = null, CancellationToken ct = default)
    {
        if (_user.UserId is not { } uid)
            throw new ForbiddenException("aprovacao_so_utilizador", "Só o dono pode aprovar publicações.");
        var p = await Load(id, ct);
        if (p.Estado != ConteudoEstado.PorAprovar)
            throw new ConflictException("conteudo_estado_invalido", $"Esta publicação está «{Nome(p.Estado)}», não «Por aprovar».");
        if (hora is not null) p.AgendadoPara = ResolverHora(hora);
        ValidarParaAprovacao(p);
        await ExigirOpcoesTikTok(p, ct);
        p.Estado = ConteudoEstado.Aprovada;
        p.AprovadoEm = DateTime.UtcNow;
        p.AprovadoPorUserId = uid;
        p.MotivoRejeicao = null;
        await _repo.SaveAsync(ct);
        await Auditar(AuditAction.Update, p, new { estado = "Aprovada" }, ct);
        return ToDto(p);
    }

    public async Task<ConteudoPublicacaoDto> RejeitarAsync(Guid id, string? motivo, CancellationToken ct = default)
    {
        if (_user.UserId is null)
            throw new ForbiddenException("aprovacao_so_utilizador", "Só o dono pode rejeitar publicações.");
        var p = await Load(id, ct);
        if (p.Estado is not (ConteudoEstado.PorAprovar or ConteudoEstado.Aprovada))
            throw new ConflictException("conteudo_estado_invalido", $"Esta publicação está «{Nome(p.Estado)}» e já não pode ser rejeitada.");
        var m = TrimOrNull(motivo);
        if (m is { Length: > 1000 }) throw new ValidationException("motivo_longo", "Motivo até 1000 caracteres.");
        p.Estado = ConteudoEstado.Rejeitada;
        p.MotivoRejeicao = m;
        p.AprovadoEm = null;
        p.AprovadoPorUserId = null;
        await _repo.SaveAsync(ct);
        await Auditar(AuditAction.Update, p, new { estado = "Rejeitada", motivo = m }, ct);
        return ToDto(p);
    }

    public async Task<ConteudoPublicacaoDto> MudarHoraAsync(Guid id, HoraPublicacaoRequest req, CancellationToken ct = default)
    {
        // Só o dono: a chave do Claude muda a hora pelo PUT normal, que devolve a publicação à fila.
        if (_user.UserId is null)
            throw new ForbiddenException("hora_so_utilizador", "Só o dono pode mudar a hora sem nova aprovação.");
        var p = await Load(id, ct);
        if (p.Estado is not (ConteudoEstado.PorAprovar or ConteudoEstado.Aprovada))
            throw new ConflictException("conteudo_estado_invalido", $"Uma publicação «{Nome(p.Estado)}» já não muda de hora.");
        var antes = p.AgendadoPara;
        p.AgendadoPara = ResolverHora(req);
        await _repo.SaveAsync(ct);
        await Auditar(AuditAction.Update, p, new { agendadoPara = p.AgendadoPara, antes }, ct);
        return ToDto(p);
    }

    /// <summary>«Agora» sai no próximo minuto (o publicador corre de minuto a minuto). Horas já passadas são recusadas.</summary>
    private static DateTime ResolverHora(HoraPublicacaoRequest req)
    {
        if (req.Agora) return DateTime.UtcNow;
        var h = ToUtc(req.AgendadoPara) ?? throw new ValidationException("hora_obrigatoria", "Escolhe a hora ou usa «Publicar já».");
        if (h < DateTime.UtcNow.AddMinutes(-1))
            throw new ValidationException("hora_passada", "Essa hora já passou — escolhe outra ou usa «Publicar já».");
        return h;
    }

    public async Task<ConteudoPublicacaoDto> CancelarAsync(Guid id, CancellationToken ct = default)
    {
        var p = await Load(id, ct);
        if (p.Estado is ConteudoEstado.Publicada or ConteudoEstado.Cancelada or ConteudoEstado.APublicar or ConteudoEstado.Removida)
            throw new ConflictException("conteudo_estado_invalido", $"Uma publicação «{Nome(p.Estado)}» não pode ser cancelada.");
        p.Estado = ConteudoEstado.Cancelada;
        await _repo.SaveAsync(ct);
        await Auditar(AuditAction.Update, p, new { estado = "Cancelada" }, ct);
        return ToDto(p);
    }

    public async Task<ConteudoPublicacaoDto> MarcarRemovidaAsync(Guid id, CancellationToken ct = default)
    {
        var p = await Load(id, ct);
        if (p.Estado == ConteudoEstado.Removida) return ToDto(p);
        if (p.Estado is not (ConteudoEstado.Publicada or ConteudoEstado.ComFalhas))
            throw new ConflictException("conteudo_estado_invalido", $"Só se marca como removida uma publicação que já saiu (esta está «{Nome(p.Estado)}»).");
        p.Estado = ConteudoEstado.Removida;
        await _repo.SaveAsync(ct);
        await Auditar(AuditAction.Update, p, new { estado = "Removida" }, ct);
        return ToDto(p);
    }

    public async Task<ConteudoPublicacaoDto> AtualizarDestinoAsync(Guid id, RedeSocial rede, AtualizarDestinoRequest req, CancellationToken ct = default)
    {
        var p = await Load(id, ct);
        if (p.Estado is not (ConteudoEstado.Aprovada or ConteudoEstado.APublicar or ConteudoEstado.ComFalhas or ConteudoEstado.Publicada))
            throw new ConflictException("conteudo_nao_aprovado", "Só se regista a publicação de conteúdo aprovado.");
        var d = p.Destinos.FirstOrDefault(x => x.Rede == rede)
                ?? throw new NotFoundException("ConteudoDestino", $"{p.Numero}/{rede}");
        if (d.Estado == ConteudoDestinoEstado.Publicado && req.Estado != ConteudoDestinoEstado.Publicado)
            throw new ConflictException("destino_ja_publicado", $"Já saiu no {rede}; não se volta atrás.");

        d.Estado = req.Estado;
        d.ExternalId = TrimOrNull(req.ExternalId) ?? d.ExternalId;
        d.Permalink = TrimOrNull(req.Permalink) ?? d.Permalink;
        if (d.Permalink is { Length: > 500 }) throw new ValidationException("permalink_longo", "Link até 500 caracteres.");
        if (req.Estado == ConteudoDestinoEstado.Falhou)
        {
            d.Tentativas++;
            d.UltimoErro = Truncar(TrimOrNull(req.Erro) ?? "Erro sem detalhe.", 2000);
        }
        else
        {
            d.UltimoErro = null;
        }
        if (req.Estado is ConteudoDestinoEstado.Publicado or ConteudoDestinoEstado.AgendadoNaRede)
            d.PublicadoEm ??= req.Estado == ConteudoDestinoEstado.Publicado ? DateTime.UtcNow : p.AgendadoPara;

        RecalcularEstado(p);
        await _repo.SaveAsync(ct);
        await Auditar(AuditAction.Update, p, new { rede = rede.ToString(), destino = req.Estado.ToString(), estado = p.Estado.ToString() }, ct);
        return ToDto(p);
    }

    // ---- TikTok (Sprint 603) --------------------------------------------------------------------

    public static readonly IReadOnlySet<string> PrivacidadesTikTok =
        new HashSet<string> { "PUBLIC_TO_EVERYONE", "MUTUAL_FOLLOW_FRIENDS", "FOLLOWER_OF_CREATOR", "SELF_ONLY" };

    /// <summary>
    /// Guarda as escolhas do dono para o TikTok. É o próprio dono no ecrã de aprovação (não muda o
    /// estado: escolher a privacidade faz parte de aprovar). Validações = regras do TikTok.
    /// </summary>
    public async Task<ConteudoPublicacaoDto> GuardarOpcoesTikTokAsync(Guid id, GuardarOpcoesTikTokRequest req, CancellationToken ct = default)
    {
        if (_user.UserId is null)
            throw new ForbiddenException("aprovacao_so_utilizador", "Só o dono escolhe as opções do TikTok.");
        var p = await Load(id, ct);
        if (p.Estado is not (ConteudoEstado.Rascunho or ConteudoEstado.PorAprovar or ConteudoEstado.Aprovada))
            throw new ConflictException("conteudo_estado_invalido", $"Esta publicação está «{Nome(p.Estado)}» e já não se muda.");
        var d = p.Destinos.FirstOrDefault(x => x.Rede == RedeSocial.TikTok)
                ?? throw new ValidationException("sem_tiktok", "Esta publicação não vai para o TikTok.");
        if (d.Estado != ConteudoDestinoEstado.Pendente)
            throw new ConflictException("destino_ja_tratado", "Já saiu (ou foi agendado) no TikTok.");

        var titulo = TrimOrNull(req.Titulo);
        if (titulo is { Length: > 2200 }) throw new ValidationException("titulo_longo", "No TikTok o texto vai até 2200 caracteres.");
        var privacidade = TrimOrNull(req.Privacidade);
        if (privacidade is not null && !PrivacidadesTikTok.Contains(privacidade))
            throw new ValidationException("privacidade_invalida", "Privacidade do TikTok desconhecida.");
        if (req.Divulgacao && !req.SuaMarca && !req.ConteudoPatrocinado)
            throw new ValidationException("divulgacao_incompleta", "Indica se o conteúdo promove a tua marca, uma marca de terceiros, ou ambas.");
        if (req.Divulgacao && req.ConteudoPatrocinado && privacidade == "SELF_ONLY")
            throw new ValidationException("patrocinado_privado", "Conteúdo patrocinado não pode ficar «Só eu».");

        d.LegendaPropria = titulo;
        d.OpcoesJson = privacidade is null ? null : System.Text.Json.JsonSerializer.Serialize(new TikTokOpcoes(
            privacidade, req.PermitirComentarios, req.PermitirDueto, req.PermitirStitch,
            req.Divulgacao, req.Divulgacao && req.SuaMarca, req.Divulgacao && req.ConteudoPatrocinado));
        await _repo.SaveAsync(ct);
        await Auditar(AuditAction.Update, p, new { tiktok = privacidade ?? "(por escolher)" }, ct);
        return ToDto(p);
    }

    public static TikTokOpcoes? LerOpcoesTikTok(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return System.Text.Json.JsonSerializer.Deserialize<TikTokOpcoes>(json); }
        catch (System.Text.Json.JsonException) { return null; }
    }

    /// <summary>
    /// Um vídeo com TikTok automático (conta ligada) só se aprova com a privacidade escolhida — o
    /// TikTok não permite publicar com uma escolha por omissão.
    /// </summary>
    private async Task ExigirOpcoesTikTok(ConteudoPublicacao p, CancellationToken ct)
    {
        if (p.Formato != ConteudoFormato.Reel) return;
        var d = p.Destinos.FirstOrDefault(x => x.Rede == RedeSocial.TikTok && x.Estado == ConteudoDestinoEstado.Pendente);
        if (d is null || LerOpcoesTikTok(d.OpcoesJson) is not null) return;
        var ligado = (await _repo.ListLigacoesAsync(p.TenantId, ct)).Any(l => l.Rede == RedeSocial.TikTok && l.Valida);
        if (ligado)
            throw new ValidationException("tiktok_sem_opcoes", "Escolhe a privacidade do TikTok antes de aprovar.");
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var p = await Load(id, ct);
        if (p.Estado is not (ConteudoEstado.Rascunho or ConteudoEstado.Rejeitada or ConteudoEstado.Cancelada))
            throw new ConflictException("conteudo_nao_apagavel", "Só se apagam rascunhos, rejeitadas ou canceladas — cancela primeiro.");
        foreach (var d in p.Destinos.ToList()) _repo.Remove(d);
        _repo.Remove(p);
        await _repo.SaveAsync(ct);
        await Auditar(AuditAction.Delete, p, new { p.Titulo }, ct);
    }

    // ---- Biblioteca e uploads -----------------------------------------------------------------

    public async Task<IReadOnlyList<ConteudoFicheiroDto>> BibliotecaAsync(bool soPorProcessar, CancellationToken ct = default)
        => (await _repo.ListBibliotecaAsync(soPorProcessar, ct)).Select(ToDto).ToList();

    public async Task<UploadTicketDto> PedirUploadAsync(PedirUploadRequest req, CancellationToken ct = default)
    {
        var nome = TrimOrNull(req.NomeOriginal) ?? throw new ValidationException("nome_obrigatorio", "Nome do ficheiro obrigatório.");
        if (!TiposPermitidos.TryGetValue(req.ContentType ?? "", out var tipo))
            throw new ValidationException("tipo_nao_suportado", "Só imagens JPG/PNG/WebP e vídeos MP4/MOV.");
        if (req.Bytes <= 0 || req.Bytes > MaxBytes)
            throw new ValidationException("tamanho_invalido", "O ficheiro tem de ter entre 1 byte e 5 GB.");

        var tenantId = TenantId;
        var id = Guid.NewGuid();
        var agora = DateTime.UtcNow;
        var f = new ConteudoFicheiro
        {
            Id = id,
            TenantId = tenantId,
            Tipo = tipo.Tipo,
            Estado = ConteudoFicheiroEstado.AEnviar,
            StorageKey = $"conteudo/{tenantId:N}/{agora:yyyy}/{agora:MM}/{id:N}{tipo.Ext}",
            ContentType = req.ContentType!.ToLowerInvariant(),
            NomeOriginal = Truncar(nome, 255)!,
            Bytes = req.Bytes,
            Notas = Truncar(TrimOrNull(req.Notas), 1000),
        };
        await _repo.AddAsync(f, ct);
        await _repo.SaveAsync(ct);

        var expira = DateTimeOffset.UtcNow.Add(ValidadeUpload);
        var url = _direct is not null
            ? _direct.CreateUploadUrl(f.StorageKey, f.ContentType, ValidadeUpload)
            : _signer.CreatePath(f.Id, "up", expira);
        return new UploadTicketDto(f.Id, url, "PUT", f.ContentType, expira.UtcDateTime);
    }

    public async Task<ConteudoFicheiroDto> ConfirmarUploadAsync(Guid ficheiroId, ConfirmarUploadRequest req, CancellationToken ct = default)
    {
        var f = await _repo.FindFicheiroAsync(ficheiroId, ct) ?? throw new NotFoundException("ConteudoFicheiro", ficheiroId);
        if (f.Estado == ConteudoFicheiroEstado.Pronto) return ToDto(f); // idempotente

        long? tamanho = _direct is not null
            ? await _direct.GetSizeAsync(f.StorageKey, ct)
            : await _storage.ExistsAsync(f.StorageKey, ct) ? f.Bytes : null;
        if (tamanho is null or 0)
            throw new ValidationException("upload_em_falta", "O ficheiro ainda não chegou ao armazenamento — tenta enviar outra vez.");

        f.Bytes = tamanho.Value;
        f.Largura = req.Largura is > 0 ? req.Largura : null;
        f.Altura = req.Altura is > 0 ? req.Altura : null;
        f.DuracaoSeg = req.DuracaoSeg is > 0 ? Math.Round(req.DuracaoSeg.Value, 2) : null;
        f.Estado = ConteudoFicheiroEstado.Pronto;
        await _repo.SaveAsync(ct);
        return ToDto(f);
    }

    public async Task<ConteudoFicheiroDto> MarcarProcessadoAsync(Guid ficheiroId, CancellationToken ct = default)
    {
        var f = await _repo.FindFicheiroAsync(ficheiroId, ct) ?? throw new NotFoundException("ConteudoFicheiro", ficheiroId);
        f.ProcessadoEm ??= DateTime.UtcNow;
        await _repo.SaveAsync(ct);
        return ToDto(f);
    }

    public async Task DeleteFicheiroAsync(Guid ficheiroId, CancellationToken ct = default)
    {
        var f = await _repo.FindFicheiroAsync(ficheiroId, ct) ?? throw new NotFoundException("ConteudoFicheiro", ficheiroId);
        if (f.PublicacaoId is { } pubId)
        {
            var p = await _repo.FindPublicacaoAsync(pubId, ct);
            if (p is not null && p.Estado is not (ConteudoEstado.Rascunho or ConteudoEstado.Rejeitada or ConteudoEstado.Cancelada))
                throw new ConflictException("ficheiro_em_uso", $"Este ficheiro faz parte da publicação #{p.Numero} («{Nome(p.Estado)}»).");
        }
        _repo.Remove(f);
        await _repo.SaveAsync(ct);
        await _storage.DeleteAsync(f.StorageKey, ct); // idempotente; os vídeos brutos ocupam muito
    }

    public async Task<(Stream Conteudo, string ContentType, string Nome)> AbrirBinarioAsync(Guid ficheiroId, CancellationToken ct = default)
    {
        var f = await _repo.FindFicheiroAsync(ficheiroId, ct) ?? throw new NotFoundException("ConteudoFicheiro", ficheiroId);
        return (await _storage.DownloadAsync(f.StorageKey, ct), f.ContentType, f.NomeOriginal);
    }

    public async Task GravarBinarioAsync(Guid ficheiroId, Stream conteudo, string contentType, CancellationToken ct = default)
    {
        var f = await _repo.FindFicheiroAsync(ficheiroId, ct) ?? throw new NotFoundException("ConteudoFicheiro", ficheiroId);
        if (f.Estado != ConteudoFicheiroEstado.AEnviar)
            throw new ConflictException("ficheiro_ja_enviado", "Este ficheiro já foi enviado.");
        if (!string.Equals(contentType, f.ContentType, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("tipo_diferente", "O tipo do ficheiro não corresponde ao pedido.");
        await _storage.UploadAsync(f.StorageKey, conteudo, f.ContentType, ct);
    }

    // ---- Regras -------------------------------------------------------------------------------

    private async Task Aplicar(ConteudoPublicacao p, GuardarPublicacaoRequest req, CancellationToken ct)
    {
        var titulo = (req.Titulo ?? "").Trim();
        if (titulo.Length is < 2 or > 200)
            throw new ValidationException("titulo_invalido", "Título obrigatório (2 a 200 caracteres).");
        var legenda = TrimOrNull(req.Legenda);
        if (legenda is { Length: > 5000 }) throw new ValidationException("legenda_longa", "Legenda até 5000 caracteres.");
        // As notas levam o guião do reel: nunca cortar em silêncio (um guião truncado não serve).
        var notas = TrimOrNull(req.Notas);
        if (notas is { Length: > MaxNotas }) throw new ValidationException("notas_longas", $"Notas/guião até {MaxNotas} caracteres.");
        if (req.AutoAprovada && (req.Formato != ConteudoFormato.Post || req.ArtigoId is null))
            throw new ValidationException("auto_aprovacao_invalida", "Só a foto promo de um artigo pode sair sem aprovação.");

        p.Linha = req.Linha;
        p.Formato = req.Formato;
        p.Titulo = titulo;
        p.Legenda = legenda;
        p.AgendadoPara = ToUtc(req.AgendadoPara);
        p.Notas = notas;
        p.ArtigoId = req.ArtigoId;

        // Ficheiros: a ordem do pedido é a ordem dos slides. Os que saem voltam à biblioteca.
        var ids = (req.FicheiroIds ?? Array.Empty<Guid>()).Distinct().ToList();
        if (ids.Count > 10) throw new ValidationException("ficheiros_a_mais", "Máximo 10 ficheiros por publicação.");
        var ficheiros = await _repo.FindFicheirosAsync(ids, ct);
        foreach (var id in ids.Where(id => ficheiros.All(f => f.Id != id)))
            throw new NotFoundException("ConteudoFicheiro", id);
        foreach (var f in ficheiros.Where(f => f.PublicacaoId is not null && f.PublicacaoId != p.Id))
            throw new ConflictException("ficheiro_noutra_publicacao", $"«{f.NomeOriginal}» já pertence a outra publicação.");
        foreach (var antigo in p.Ficheiros.Where(f => !ids.Contains(f.Id)).ToList())
        {
            antigo.PublicacaoId = null;
            p.Ficheiros.Remove(antigo);
        }
        for (var i = 0; i < ids.Count; i++)
        {
            var f = ficheiros.First(x => x.Id == ids[i]);
            f.Ordem = i;
            f.ProcessadoEm ??= DateTime.UtcNow;
            if (!p.Ficheiros.Contains(f)) p.Ficheiros.Add(f);
        }
        p.Ficheiros.Sort((a, b) => a.Ordem.CompareTo(b.Ordem));

        // Destinos: uma entrada por rede. Só se removem redes que ainda não saíram.
        var destinos = (req.Destinos ?? Array.Empty<ConteudoDestinoInput>())
            .GroupBy(d => d.Rede).Select(g => g.Last()).ToList();
        foreach (var d in p.Destinos.Where(d => destinos.All(x => x.Rede != d.Rede)).ToList())
        {
            if (d.Estado != ConteudoDestinoEstado.Pendente)
                throw new ConflictException("destino_ja_publicado", $"Já saiu no {d.Rede}; não se pode retirar essa rede.");
            p.Destinos.Remove(d);
            _repo.Remove(d);
        }
        foreach (var input in destinos)
        {
            var legendaPropria = TrimOrNull(input.LegendaPropria);
            if (legendaPropria is { Length: > 5000 }) throw new ValidationException("legenda_longa", "Legenda até 5000 caracteres.");
            var d = p.Destinos.FirstOrDefault(x => x.Rede == input.Rede);
            if (d is null)
                p.Destinos.Add(new ConteudoDestino { TenantId = p.TenantId, Rede = input.Rede, LegendaPropria = legendaPropria });
            else
                d.LegendaPropria = legendaPropria;
        }
    }

    /// <summary>O que tem de estar certo antes de o dono ver a publicação (Doc 95 §4).</summary>
    private static void ValidarParaAprovacao(ConteudoPublicacao p)
    {
        if (p.Destinos.Count == 0)
            throw new ValidationException("sem_redes", "Escolhe pelo menos uma rede.");
        if (p.AgendadoPara is null)
            throw new ValidationException("sem_hora", "Define a data e hora de publicação.");
        if (p.Ficheiros.Any(f => f.Estado != ConteudoFicheiroEstado.Pronto))
            throw new ValidationException("ficheiro_por_enviar", "Há ficheiros que ainda não acabaram de subir.");
        if (p.Ficheiros.FirstOrDefault(f => !TiposPublicaveis.Contains(f.ContentType)) is { } bruto)
            throw new ValidationException("formato_nao_publicavel", $"«{bruto.NomeOriginal}» tem de ser convertido para JPG/MP4 antes de publicar.");

        var n = p.Ficheiros.Count;
        var videos = p.Ficheiros.Count(f => f.Tipo == ConteudoFicheiroTipo.Video);
        switch (p.Formato)
        {
            case ConteudoFormato.Texto:
                if (n > 0) throw new ValidationException("texto_com_ficheiros", "Uma publicação só de texto não leva ficheiros.");
                if (p.Destinos.Any(d => d.Rede != RedeSocial.Threads))
                    throw new ValidationException("texto_so_threads", "Publicações só de texto saem apenas no Threads.");
                break;
            case ConteudoFormato.Post:
                if (n != 1 || videos != 0) throw new ValidationException("post_uma_imagem", "Um post leva exatamente 1 imagem.");
                break;
            case ConteudoFormato.Carrossel:
                if (n is < 2 or > 10) throw new ValidationException("carrossel_slides", "Um carrossel leva entre 2 e 10 slides.");
                break;
            case ConteudoFormato.Reel:
                if (n != 1 || videos != 1) throw new ValidationException("reel_um_video", "Um reel leva exatamente 1 vídeo.");
                break;
            case ConteudoFormato.Story:
                if (n != 1) throw new ValidationException("story_um_ficheiro", "Uma story leva exatamente 1 imagem ou vídeo.");
                break;
        }
        if (p.Destinos.Any(d => d.Rede == RedeSocial.TikTok) && p.Formato is not (ConteudoFormato.Reel or ConteudoFormato.Carrossel))
            throw new ValidationException("tiktok_formato", "No TikTok só saem reels ou carrosséis de fotos.");
        if (p.Destinos.Any(d => d.Rede == RedeSocial.YouTube) && p.Formato != ConteudoFormato.Reel)
            throw new ValidationException("youtube_formato", "No YouTube só saem vídeos (Shorts).");

        foreach (var d in p.Destinos)
        {
            var texto = d.LegendaPropria ?? p.Legenda;
            var limite = d.Rede switch { RedeSocial.Threads => 500, RedeSocial.Instagram => 2200, RedeSocial.TikTok => 2200, _ => 5000 };
            if (texto is not null && texto.Length > limite)
                throw new ValidationException("legenda_longa_rede", $"A legenda do {d.Rede} tem {texto.Length} caracteres (máx. {limite}).");
            if (p.Formato == ConteudoFormato.Texto && string.IsNullOrWhiteSpace(texto))
                throw new ValidationException("texto_vazio", "Escreve o texto da publicação.");
        }
    }

    /// <summary>Partilhado com o publicador automático (Fase B).</summary>
    internal static void RecalcularEstado(ConteudoPublicacao p)
    {
        if (p.Destinos.Count == 0) return;
        if (p.Destinos.All(d => d.Estado is ConteudoDestinoEstado.Publicado or ConteudoDestinoEstado.AgendadoNaRede))
        {
            p.Estado = ConteudoEstado.Publicada;
            p.PublicadaEm = p.Destinos.Max(d => d.PublicadoEm) ?? DateTime.UtcNow;
        }
        else if (p.Destinos.Any(d => d.Estado == ConteudoDestinoEstado.Falhou))
        {
            p.Estado = ConteudoEstado.ComFalhas;
        }
    }

    private async Task AvisarPorAprovar(CancellationToken ct)
    {
        var n = await _repo.CountPorAprovarAsync(ct);
        if (n == 0) return;
        // Tag fixa: no telemóvel a notificação nova substitui a anterior em vez de acumular.
        await _push.EnqueueAsync(new StaffPushJob(
            TenantId,
            "📣 Conteúdo para aprovar",
            n == 1 ? "1 publicação à espera da tua aprovação." : $"{n} publicações à espera da tua aprovação.",
            "/conteudo",
            "conteudo-aprovar"), ct);
    }

    private Task Auditar(AuditAction acao, ConteudoPublicacao p, object dados, CancellationToken ct)
        => _audit.LogAsync(acao, "ConteudoPublicacao", p.Id, dados, p.TenantId, _user.UserId, ct);

    private async Task<ConteudoPublicacao> Load(Guid id, CancellationToken ct)
        => await _repo.FindPublicacaoAsync(id, ct) ?? throw new NotFoundException("ConteudoPublicacao", id);

    // ---- Mapeamento ---------------------------------------------------------------------------

    private ConteudoPublicacaoDto ToDto(ConteudoPublicacao p) => new(
        p.Id, p.Numero, p.Linha, p.Formato, p.Titulo, p.Legenda, p.AgendadoPara, p.Estado,
        p.AprovadoEm, p.MotivoRejeicao, p.AutoAprovada, p.Origem, p.ArtigoId, p.Notas, p.PublicadaEm,
        p.CreatedAt, p.UpdatedAt,
        p.Ficheiros.OrderBy(f => f.Ordem).Select(ToDto).ToList(),
        p.Destinos.OrderBy(d => d.Rede).Select(d => new ConteudoDestinoDto(
            d.Id, d.Rede, d.LegendaPropria, d.Estado, d.ExternalId, d.Permalink, d.Tentativas, d.UltimoErro, d.PublicadoEm,
            d.Rede == RedeSocial.TikTok ? LerOpcoesTikTok(d.OpcoesJson) : null)).ToList());

    private ConteudoFicheiroDto ToDto(ConteudoFicheiro f) => new(
        f.Id, f.PublicacaoId, f.Ordem, f.Tipo, f.Estado, f.NomeOriginal, f.ContentType, f.Bytes,
        f.Largura, f.Altura, f.DuracaoSeg, f.Notas, f.ProcessadoEm, f.CreatedAt,
        f.Estado == ConteudoFicheiroEstado.Pronto ? UrlLeitura(f) : null);

    private string UrlLeitura(ConteudoFicheiro f)
        => _direct is not null
            ? _direct.CreateDownloadUrl(f.StorageKey, ValidadeLeitura)
            : _signer.CreatePath(f.Id, "down", DateTimeOffset.UtcNow.Add(ValidadeLeitura));

    private static string Nome(ConteudoEstado e) => e switch
    {
        ConteudoEstado.Rascunho => "Rascunho",
        ConteudoEstado.PorAprovar => "Por aprovar",
        ConteudoEstado.Aprovada => "Aprovada",
        ConteudoEstado.APublicar => "A publicar",
        ConteudoEstado.Publicada => "Publicada",
        ConteudoEstado.ComFalhas => "Com falhas",
        ConteudoEstado.Rejeitada => "Rejeitada",
        ConteudoEstado.Cancelada => "Cancelada",
        ConteudoEstado.Removida => "Removida",
        _ => e.ToString(),
    };

    private static DateTime? ToUtc(DateTime? d) => d is not { } v ? null : v.Kind switch
    {
        DateTimeKind.Utc => v,
        DateTimeKind.Local => v.ToUniversalTime(),
        _ => DateTime.SpecifyKind(v, DateTimeKind.Utc),
    };

    private static string? TrimOrNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string? Truncar(string? s, int max) => s is null || s.Length <= max ? s : s[..max];
}
