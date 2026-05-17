using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Diagnostico;

public interface IDiagnosticoService
{
    Task<IReadOnlyList<DiagnosticoTemplateDto>> ListTemplatesAsync(CancellationToken ct = default);
    Task<DiagnosticoTemplateDto> CreateTemplateAsync(CreateTemplateRequest req, CancellationToken ct = default);
    Task DeleteTemplateAsync(Guid id, CancellationToken ct = default);

    Task<DiagnosticoExecucaoDto?> GetByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default);
    Task<DiagnosticoExecucaoDto> StartAsync(Guid reparacaoId, StartExecucaoRequest req, CancellationToken ct = default);
    Task<DiagnosticoExecucaoDto> UpdateAsync(Guid reparacaoId, UpdateExecucaoRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid reparacaoId, CancellationToken ct = default);
}

public class DiagnosticoService : IDiagnosticoService
{
    private readonly IDiagnosticoRepository _repo;
    private readonly IReparacaoRepository _reparacoes;

    public DiagnosticoService(IDiagnosticoRepository repo, IReparacaoRepository reparacoes)
    {
        _repo = repo;
        _reparacoes = reparacoes;
    }

    public async Task<IReadOnlyList<DiagnosticoTemplateDto>> ListTemplatesAsync(CancellationToken ct = default)
    {
        var rows = await _repo.ListTemplatesAsync(ct);
        return rows.Select(ToTemplateDto).ToList();
    }

    public async Task<DiagnosticoTemplateDto> CreateTemplateAsync(CreateTemplateRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Nome))
            throw new ValidationException("nome_required", "Nome do template é obrigatório.");
        if (req.Items.Count == 0)
            throw new ValidationException("items_required", "Adiciona pelo menos um item ao checklist.");

        var template = new DiagnosticoTemplate
        {
            Nome = req.Nome.Trim(),
            Categoria = req.Categoria,
            IsDefault = req.IsDefault,
            Activo = true,
            Items = req.Items.Select((x, i) => new DiagnosticoTemplateItem
            {
                Label = x.Label.Trim(),
                Descricao = string.IsNullOrWhiteSpace(x.Descricao) ? null : x.Descricao.Trim(),
                Grupo = string.IsNullOrWhiteSpace(x.Grupo) ? null : x.Grupo.Trim(),
                Ordem = x.Ordem == 0 ? i : x.Ordem,
                Peso = Math.Clamp(x.Peso == 0 ? 5 : x.Peso, 1, 10),
            }).ToList(),
        };
        await _repo.AddTemplateAsync(template, ct);
        await _repo.SaveAsync(ct);
        return ToTemplateDto(template);
    }

    public async Task DeleteTemplateAsync(Guid id, CancellationToken ct = default)
    {
        var t = await _repo.FindTemplateAsync(id, ct) ?? throw new NotFoundException("DiagnosticoTemplate", id);
        _repo.RemoveTemplate(t);
        await _repo.SaveAsync(ct);
    }

    public async Task<DiagnosticoExecucaoDto?> GetByReparacaoAsync(Guid reparacaoId, CancellationToken ct = default)
    {
        var e = await _repo.FindExecucaoByReparacaoAsync(reparacaoId, ct);
        return e is null ? null : ToExecucaoDto(e);
    }

    public async Task<DiagnosticoExecucaoDto> StartAsync(Guid reparacaoId, StartExecucaoRequest req, CancellationToken ct = default)
    {
        var rep = await _reparacoes.FindByIdAsync(reparacaoId, ct)
            ?? throw new NotFoundException("Reparacao", reparacaoId);

        var existente = await _repo.FindExecucaoByReparacaoAsync(reparacaoId, ct);
        if (existente is not null)
            throw new ConflictException("ja_existe", "Esta reparação já tem um diagnóstico iniciado. Apaga primeiro se quiseres começar do zero.");

        DiagnosticoTemplate? template = null;
        if (req.TemplateId is { } tid)
            template = await _repo.FindTemplateAsync(tid, ct) ?? throw new NotFoundException("DiagnosticoTemplate", tid);
        else
        {
            var cat = req.Categoria ?? DeviceCategory.Smartphone;
            template = await _repo.FindDefaultTemplateAsync(cat, ct);
            if (template is null)
                throw new NotFoundException("DiagnosticoTemplate (default)", cat);
        }

        var exec = new DiagnosticoExecucao
        {
            ReparacaoId = rep.Id,
            TemplateId = template.Id,
            TemplateNomeSnapshot = template.Nome,
            Categoria = template.Categoria,
            Items = template.Items.OrderBy(i => i.Ordem).Select(i => new DiagnosticoExecucaoItem
            {
                Label = i.Label,
                Descricao = i.Descricao,
                Grupo = i.Grupo,
                Ordem = i.Ordem,
                Peso = i.Peso,
                Resultado = DiagnosticoResultado.NaoTestado,
            }).ToList(),
        };
        await _repo.AddExecucaoAsync(exec, ct);
        await _repo.SaveAsync(ct);
        return ToExecucaoDto(exec);
    }

    public async Task<DiagnosticoExecucaoDto> UpdateAsync(Guid reparacaoId, UpdateExecucaoRequest req, CancellationToken ct = default)
    {
        var exec = await _repo.FindExecucaoByReparacaoAsync(reparacaoId, ct)
            ?? throw new NotFoundException("DiagnosticoExecucao para reparação", reparacaoId);

        // Atualiza items individuais (Patch parcial)
        var byId = exec.Items.ToDictionary(i => i.Id);
        foreach (var update in req.Items)
        {
            if (byId.TryGetValue(update.ItemId, out var item))
            {
                item.Resultado = update.Resultado;
                item.Notas = string.IsNullOrWhiteSpace(update.Notas) ? null : update.Notas.Trim();
            }
        }
        exec.NotasGerais = string.IsNullOrWhiteSpace(req.NotasGerais) ? null : req.NotasGerais.Trim();
        exec.Score = CalcularScore(exec.Items);
        if (req.MarcarCompletado && exec.CompletadoEm is null)
            exec.CompletadoEm = DateTime.UtcNow;
        await _repo.SaveAsync(ct);
        return ToExecucaoDto(exec);
    }

    public async Task DeleteAsync(Guid reparacaoId, CancellationToken ct = default)
    {
        var exec = await _repo.FindExecucaoByReparacaoAsync(reparacaoId, ct)
            ?? throw new NotFoundException("DiagnosticoExecucao", reparacaoId);
        _repo.RemoveExecucao(exec);
        await _repo.SaveAsync(ct);
    }

    /// <summary>
    /// Cálculo de Health Score 0-100, ponderado por peso.
    /// Items NaoTestado não contam (denominador menor).
    /// OK = 1.0, Marginal = 0.5, Avaria = 0.0 (do total de peso possível).
    /// </summary>
    public static int? CalcularScore(IEnumerable<DiagnosticoExecucaoItem> items)
    {
        var testados = items.Where(i => i.Resultado != DiagnosticoResultado.NaoTestado).ToList();
        if (testados.Count == 0) return null;

        var pesoTotal = testados.Sum(i => i.Peso);
        if (pesoTotal == 0) return null;

        var pontos = testados.Sum(i => i.Resultado switch
        {
            DiagnosticoResultado.Ok => i.Peso * 1.0,
            DiagnosticoResultado.Marginal => i.Peso * 0.5,
            _ => 0.0,
        });
        return (int)Math.Round(pontos / pesoTotal * 100);
    }

    private static DiagnosticoTemplateDto ToTemplateDto(DiagnosticoTemplate t) => new(
        t.Id, t.Nome, t.Categoria, t.IsDefault, t.Activo,
        t.Items.OrderBy(i => i.Ordem)
            .Select(i => new DiagnosticoTemplateItemDto(i.Id, i.Label, i.Descricao, i.Grupo, i.Ordem, i.Peso))
            .ToList());

    private static DiagnosticoExecucaoDto ToExecucaoDto(DiagnosticoExecucao e) => new(
        e.Id, e.ReparacaoId, e.TemplateId, e.TemplateNomeSnapshot, e.Categoria, e.CompletadoEm, e.NotasGerais, e.Score,
        e.Items.OrderBy(i => i.Ordem)
            .Select(i => new DiagnosticoExecucaoItemDto(i.Id, i.Label, i.Descricao, i.Grupo, i.Ordem, i.Peso, i.Resultado, i.Notas))
            .ToList());
}
