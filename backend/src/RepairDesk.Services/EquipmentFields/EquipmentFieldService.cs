using System.Text.Json;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.EquipmentFields;

public interface IEquipmentFieldService
{
    Task<IReadOnlyList<EquipmentFieldTemplateDto>> ListAsync(bool includeInactive, CancellationToken ct = default);
    Task<IReadOnlyList<EquipmentFieldTemplateDto>> ListActiveAsync(CancellationToken ct = default);
    Task<EquipmentFieldTemplateDto> CreateAsync(CreateEquipmentFieldTemplateRequest req, CancellationToken ct = default);
    Task<EquipmentFieldTemplateDto> UpdateAsync(Guid id, UpdateEquipmentFieldTemplateRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task ReorderAsync(ReorderEquipmentFieldTemplatesRequest req, CancellationToken ct = default);
    Task<IReadOnlyList<EquipmentFieldValueDto>> SetValuesAsync(Guid reparacaoId, SetEquipmentFieldValuesRequest req, CancellationToken ct = default);
    Task<IReadOnlyList<EquipmentFieldValueDto>> GetValuesAsync(Guid reparacaoId, bool visibleInPortalOnly = false, CancellationToken ct = default);
    Task EnsureDefaultsAsync(CancellationToken ct = default);
}

public class EquipmentFieldService : IEquipmentFieldService
{
    private const int MaxFields = 20;
    private const int MaxActiveTemplates = 10;
    private const int MaxSelectOptions = 50;

    private readonly IEquipmentFieldRepository _repo;
    private readonly IReparacaoRepository _reparacoes;
    private readonly ITenantContext _tenant;

    public EquipmentFieldService(IEquipmentFieldRepository repo, IReparacaoRepository reparacoes, ITenantContext tenant)
    {
        _repo = repo;
        _reparacoes = reparacoes;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<EquipmentFieldTemplateDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        await EnsureDefaultsAsync(ct);
        var items = await _repo.ListTemplatesAsync(includeInactive, ct);
        return items.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<EquipmentFieldTemplateDto>> ListActiveAsync(CancellationToken ct = default)
    {
        await EnsureDefaultsAsync(ct);
        var items = await _repo.ListActiveTemplatesAsync(ct);
        return items.Select(ToDto).ToList();
    }

    public async Task<EquipmentFieldTemplateDto> CreateAsync(CreateEquipmentFieldTemplateRequest req, CancellationToken ct = default)
    {
        var tenantId = RequireTenant();
        ValidateTemplate(req.Nome, req.Fields);
        if (req.IsActive && await _repo.CountActiveTemplatesAsync(ct) >= MaxActiveTemplates)
            throw new ValidationException("limite_templates", $"Máximo de {MaxActiveTemplates} templates activos por tenant.");

        var nextOrder = (await _repo.ListTemplatesAsync(includeInactive: true, ct)).Count;
        var template = new EquipmentFieldTemplate
        {
            TenantId = tenantId,
            Nome = req.Nome.Trim(),
            Categoria = req.Categoria,
            IsActive = req.IsActive,
            Ordem = nextOrder,
        };
        ApplyFields(template, req.Fields, tenantId);
        _repo.AddTemplate(template);
        await _repo.SaveAsync(ct);
        return ToDto(template);
    }

    public async Task<EquipmentFieldTemplateDto> UpdateAsync(Guid id, UpdateEquipmentFieldTemplateRequest req, CancellationToken ct = default)
    {
        var tenantId = RequireTenant();
        var template = await _repo.FindTemplateAsync(id, includeFields: true, ct)
            ?? throw new NotFoundException("EquipmentFieldTemplate", id);
        ValidateTemplate(req.Nome, req.Fields);

        if (!template.IsActive && req.IsActive && await _repo.CountActiveTemplatesAsync(ct) >= MaxActiveTemplates)
            throw new ValidationException("limite_templates", $"Máximo de {MaxActiveTemplates} templates activos por tenant.");

        template.Nome = req.Nome.Trim();
        template.Categoria = req.Categoria;
        template.IsActive = req.IsActive;
        ApplyFields(template, req.Fields, tenantId);
        await _repo.SaveAsync(ct);
        return ToDto(template);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _repo.FindTemplateAsync(id, includeFields: true, ct)
            ?? throw new NotFoundException("EquipmentFieldTemplate", id);
        if (await _repo.HasActiveReparacoesUsingTemplateAsync(id, ct))
            throw new ConflictException("template_em_uso", "Este template está usado por reparações activas. Desactiva-o em vez de apagar.");

        _repo.RemoveTemplate(template);
        await _repo.SaveAsync(ct);
    }

    public async Task ReorderAsync(ReorderEquipmentFieldTemplatesRequest req, CancellationToken ct = default)
    {
        var templates = (await _repo.ListTemplatesAsync(includeInactive: true, ct)).ToDictionary(t => t.Id);
        for (var i = 0; i < req.Ids.Count; i++)
        {
            if (templates.TryGetValue(req.Ids[i], out var template))
                template.Ordem = i;
        }
        await _repo.SaveAsync(ct);
    }

    public async Task<IReadOnlyList<EquipmentFieldValueDto>> SetValuesAsync(Guid reparacaoId, SetEquipmentFieldValuesRequest req, CancellationToken ct = default)
    {
        var tenantId = RequireTenant();
        var reparacao = await _reparacoes.FindByIdAsync(reparacaoId, ct)
            ?? throw new NotFoundException("Reparacao", reparacaoId);

        EquipmentFieldTemplate? template = null;
        if (req.TemplateId is not null)
        {
            template = await _repo.FindTemplateAsync(req.TemplateId.Value, includeFields: true, ct)
                ?? throw new NotFoundException("EquipmentFieldTemplate", req.TemplateId.Value);
            if (!template.IsActive && reparacao.EquipmentFieldTemplateId != template.Id)
                throw new ConflictException("template_inactivo", "Este template está inactivo.");
        }

        var definitions = template?.Fields.OrderBy(f => f.Ordem).ToList() ?? new List<EquipmentFieldDefinition>();
        ValidateValues(definitions, req.Values);

        var incoming = req.Values.ToDictionary(v => v.FieldDefinitionId, v => NormalizeValue(v.Value));
        var existing = (await _repo.ListValuesByReparacaoAsync(reparacaoId, includeDefinition: false, ct)).ToList();
        foreach (var value in existing)
        {
            if (!incoming.TryGetValue(value.FieldDefinitionId, out var next) || string.IsNullOrWhiteSpace(next))
            {
                _repo.RemoveValue(value);
                continue;
            }
            value.Value = next;
            incoming.Remove(value.FieldDefinitionId);
        }

        foreach (var (fieldId, value) in incoming)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            _repo.AddValue(new EquipmentFieldValue
            {
                TenantId = tenantId,
                ReparacaoId = reparacaoId,
                FieldDefinitionId = fieldId,
                Value = value,
            });
        }

        reparacao.EquipmentFieldTemplateId = req.TemplateId;
        await _reparacoes.SaveAsync(ct);
        return await GetValuesAsync(reparacaoId, visibleInPortalOnly: false, ct);
    }

    public async Task<IReadOnlyList<EquipmentFieldValueDto>> GetValuesAsync(Guid reparacaoId, bool visibleInPortalOnly = false, CancellationToken ct = default)
    {
        var values = visibleInPortalOnly
            ? await _repo.ListVisiblePortalValuesByReparacaoAsync(reparacaoId, ct)
            : await _repo.ListValuesByReparacaoAsync(reparacaoId, includeDefinition: true, ct);
        return values
            .Where(v => v.FieldDefinition is not null)
            .Select(v => ToValueDto(v.FieldDefinition!, v.Value))
            .ToList();
    }

    public async Task EnsureDefaultsAsync(CancellationToken ct = default)
    {
        var tenantId = RequireTenant();
        if (await _repo.AnyTemplateAsync(ct)) return;

        foreach (var template in BuildDefaultTemplates(tenantId))
            _repo.AddTemplate(template);
        await _repo.SaveAsync(ct);
    }

    private Guid RequireTenant() =>
        _tenant.TenantId ?? throw new ForbiddenException("no_tenant", "Tenant não definido.");

    private static void ValidateTemplate(string nome, IReadOnlyList<UpsertEquipmentFieldDefinitionRequest> fields)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ValidationException("nome_obrigatorio", "Nome do template é obrigatório.");
        if (nome.Length > 100)
            throw new ValidationException("nome_longo", "Nome do template não pode exceder 100 caracteres.");
        if (fields.Count > MaxFields)
            throw new ValidationException("limite_fields", $"Máximo de {MaxFields} campos por template.");

        foreach (var field in fields)
        {
            if (string.IsNullOrWhiteSpace(field.Label))
                throw new ValidationException("label_obrigatorio", "Label do campo é obrigatório.");
            if (field.Label.Length > 100)
                throw new ValidationException("label_longo", "Label do campo não pode exceder 100 caracteres.");
            if (field.Type == EquipmentFieldType.Select && (field.Options is null || field.Options.Count == 0))
                throw new ValidationException("select_sem_options", $"Campo '{field.Label}' precisa de opções.");
            if (field.Options?.Count > MaxSelectOptions)
                throw new ValidationException("limite_options", $"Máximo de {MaxSelectOptions} opções por campo select.");
        }
    }

    private static void ValidateValues(IReadOnlyList<EquipmentFieldDefinition> definitions, IReadOnlyList<SetEquipmentFieldValueRequest> values)
    {
        var map = values.ToDictionary(v => v.FieldDefinitionId, v => NormalizeValue(v.Value));
        var validIds = definitions.Select(d => d.Id).ToHashSet();
        if (map.Keys.Any(id => !validIds.Contains(id)))
            throw new ValidationException("field_invalido", "Um ou mais campos não pertencem ao template seleccionado.");

        foreach (var def in definitions)
        {
            map.TryGetValue(def.Id, out var value);
            if (def.Required && string.IsNullOrWhiteSpace(value))
                throw new ValidationException("field_required", $"Campo obrigatório em falta: {def.Label}");
            if (string.IsNullOrWhiteSpace(value)) continue;
            if (def.Type == EquipmentFieldType.Select && !ParseOptions(def.OptionsJson).Contains(value))
                throw new ValidationException("select_valor_invalido", $"Valor inválido para {def.Label}.");
            if (def.Type == EquipmentFieldType.Boolean && value is not ("true" or "false"))
                throw new ValidationException("boolean_valor_invalido", $"Valor inválido para {def.Label}.");
            if (def.Type == EquipmentFieldType.Number && !decimal.TryParse(value.Replace(',', '.'), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out _))
                throw new ValidationException("number_valor_invalido", $"Valor inválido para {def.Label}.");
        }
    }

    private static void ApplyFields(EquipmentFieldTemplate template, IReadOnlyList<UpsertEquipmentFieldDefinitionRequest> fields, Guid tenantId)
    {
        var byId = template.Fields.ToDictionary(f => f.Id);
        var keep = new HashSet<Guid>();

        for (var i = 0; i < fields.Count; i++)
        {
            var req = fields[i];
            var ordem = req.Ordem >= 0 ? req.Ordem : i;
            if (req.Id is not null && byId.TryGetValue(req.Id.Value, out var existing))
            {
                ApplyField(existing, req, ordem);
                keep.Add(existing.Id);
            }
            else
            {
                var field = new EquipmentFieldDefinition
                {
                    TenantId = tenantId,
                    Label = req.Label.Trim(),
                    Type = req.Type,
                    OptionsJson = SerializeOptions(req.Type, req.Options),
                    Required = req.Required,
                    Ordem = ordem,
                    VisibleInPortal = req.VisibleInPortal,
                };
                template.Fields.Add(field);
                keep.Add(field.Id);
            }
        }

        foreach (var stale in template.Fields.Where(f => !keep.Contains(f.Id)).ToList())
            stale.IsDeleted = true;
    }

    private static void ApplyField(EquipmentFieldDefinition field, UpsertEquipmentFieldDefinitionRequest req, int ordem)
    {
        field.Label = req.Label.Trim();
        field.Type = req.Type;
        field.OptionsJson = SerializeOptions(req.Type, req.Options);
        field.Required = req.Required;
        field.Ordem = ordem;
        field.VisibleInPortal = req.VisibleInPortal;
    }

    private static string? SerializeOptions(EquipmentFieldType type, IReadOnlyList<string>? options)
    {
        if (type != EquipmentFieldType.Select) return null;
        var normalized = (options ?? Array.Empty<string>())
            .Select(o => o.Trim())
            .Where(o => o.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxSelectOptions)
            .ToList();
        return JsonSerializer.Serialize(normalized);
    }

    private static IReadOnlyList<string> ParseOptions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        try { return JsonSerializer.Deserialize<List<string>>(json)?.ToArray() ?? Array.Empty<string>(); }
        catch { return Array.Empty<string>(); }
    }

    private static EquipmentFieldTemplateDto ToDto(EquipmentFieldTemplate t) =>
        new(t.Id, t.Nome, t.Categoria, t.IsActive, t.Ordem, t.Fields.Where(f => !f.IsDeleted).OrderBy(f => f.Ordem).Select(ToDefinitionDto).ToList());

    private static EquipmentFieldDefinitionDto ToDefinitionDto(EquipmentFieldDefinition f) =>
        new(f.Id, f.Label, f.Type, ParseOptions(f.OptionsJson), f.Required, f.Ordem, f.VisibleInPortal);

    private static EquipmentFieldValueDto ToValueDto(EquipmentFieldDefinition f, string? value) =>
        new(f.Id, f.Label, f.Type, value, f.Required, f.VisibleInPortal, f.Ordem);

    private static string? NormalizeValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<EquipmentFieldTemplate> BuildDefaultTemplates(Guid tenantId)
    {
        EquipmentFieldTemplate Build(string nome, DeviceCategory categoria, int ordem, params (string label, EquipmentFieldType type, bool required, bool portal)[] fields)
        {
            var template = new EquipmentFieldTemplate
            {
                TenantId = tenantId,
                Nome = nome,
                Categoria = categoria,
                IsActive = true,
                Ordem = ordem,
            };
            for (var i = 0; i < fields.Length; i++)
            {
                var (label, type, required, portal) = fields[i];
                template.Fields.Add(new EquipmentFieldDefinition
                {
                    TenantId = tenantId,
                    Label = label,
                    Type = type,
                    Required = required,
                    Ordem = i,
                    VisibleInPortal = portal,
                });
            }
            return template;
        }

        return new[]
        {
            Build("Telemóvel", DeviceCategory.Smartphone, 0,
                ("IMEI", EquipmentFieldType.Text, false, false),
                ("Marca", EquipmentFieldType.Text, false, true),
                ("Modelo", EquipmentFieldType.Text, false, true)),
            Build("Laptop", DeviceCategory.Laptop, 1,
                ("Marca", EquipmentFieldType.Text, false, true),
                ("Modelo", EquipmentFieldType.Text, false, true),
                ("CPU", EquipmentFieldType.Text, false, true),
                ("RAM", EquipmentFieldType.Text, false, true),
                ("Storage", EquipmentFieldType.Text, false, true),
                ("GPU", EquipmentFieldType.Text, false, true)),
            Build("Desktop", DeviceCategory.Desktop, 2,
                ("Marca", EquipmentFieldType.Text, false, true),
                ("Modelo", EquipmentFieldType.Text, false, true),
                ("CPU", EquipmentFieldType.Text, false, true),
                ("RAM", EquipmentFieldType.Text, false, true),
                ("Storage", EquipmentFieldType.Text, false, true),
                ("GPU", EquipmentFieldType.Text, false, true),
                ("MotherBoard", EquipmentFieldType.Text, false, true)),
        };
    }
}
