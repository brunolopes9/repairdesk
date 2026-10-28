using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Fornecedores;

public interface IFornecedorService
{
    Task<IReadOnlyList<FornecedorDto>> ListAsync(bool includeInactive, CancellationToken ct = default);
    Task<FornecedorDto> CreateAsync(FornecedorWriteRequest req, CancellationToken ct = default);
    Task<FornecedorDto> UpdateAsync(Guid id, FornecedorWriteRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    /// <summary>Sprint 579: junta um fornecedor repetido (origem) noutro (destino) — compras, faturas e despesas passam para o destino.</summary>
    Task<FornecedorDto> FundirAsync(Guid destinoId, Guid origemId, CancellationToken ct = default);
}

public sealed record FornecedorDto(
    Guid Id,
    string Name,
    string? Code,
    string? Email,
    string? RmaEmail,
    string? Phone,
    string? Website,
    int? GarantiaB2BDiasDefault,
    string? Notas,
    bool Active,
    DateTime CreatedAt,
    // Sprint 525: fornecedor intra-UE (autoliquidação) — compras não geram IVA dedutível em PT.
    bool IntraUe = false,
    RegimeIvaFornecedor RegimeIva = RegimeIvaFornecedor.Nacional,
    string? Pais = null,
    string? Nif = null);

public sealed record FornecedorWriteRequest(
    string Name,
    string? Email,
    string? RmaEmail,
    string? Phone,
    string? Website,
    int? GarantiaB2BDiasDefault,
    string? Notas,
    bool Active,
    bool IntraUe = false,
    // Regime de IVA explícito; quando null, deriva de IntraUe (clientes antigos).
    RegimeIvaFornecedor? RegimeIva = null,
    string? Pais = null,
    /// <summary>Sprint 579: NIF / nº de IVA. Null = não mexe (clientes antigos); "" = apaga.</summary>
    string? Nif = null);

public class FornecedorService : IFornecedorService
{
    private readonly IFornecedorRepository _repo;
    private readonly ITenantContext _tenant;
    private readonly IAuditLogger _audit;

    public FornecedorService(IFornecedorRepository repo, ITenantContext tenant, IAuditLogger audit)
    {
        _repo = repo;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task<IReadOnlyList<FornecedorDto>> ListAsync(bool includeInactive, CancellationToken ct = default)
    {
        var items = await _repo.ListByTenantAsync(includeInactive, ct);
        return items.Select(ToDto).ToList();
    }

    public async Task<FornecedorDto> CreateAsync(FornecedorWriteRequest req, CancellationToken ct = default)
    {
        var name = Validate(req);
        if (_tenant.TenantId is not { } tenantId)
            throw new ValidationException("no_tenant_context", "Sem contexto de tenant.");

        var existing = await _repo.FindByNameAsync(name, ct);
        if (existing is not null)
            throw new ConflictException("name_already_exists", $"Já existe um fornecedor com o nome '{name}'.");

        var entity = new Fornecedor
        {
            TenantId = tenantId,
            Name = name,
            Email = Clean(req.Email),
            RmaEmail = Clean(req.RmaEmail),
            Phone = Clean(req.Phone),
            Website = Clean(req.Website),
            GarantiaB2BDiasDefault = req.GarantiaB2BDiasDefault is > 0 ? req.GarantiaB2BDiasDefault : null,
            Notas = Clean(req.Notas),
            Active = req.Active,
            RegimeIva = ResolveRegime(req),
            Pais = CleanPais(req.Pais),
            Nif = await NifLivreAsync(req.Nif, null, ct),
        };
        await _repo.AddAsync(entity, ct);
        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Create, nameof(Fornecedor), entity.Id, new { entity.Name }, ct: ct);
        return ToDto(entity);
    }

    public async Task<FornecedorDto> UpdateAsync(Guid id, FornecedorWriteRequest req, CancellationToken ct = default)
    {
        var entity = await _repo.FindByIdAsync(id, ct)
            ?? throw new NotFoundException("Fornecedor", id);
        var name = Validate(req);

        // Se o nome mudou, verificar conflito.
        if (!string.Equals(entity.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            var conflict = await _repo.FindByNameAsync(name, ct);
            if (conflict is not null && conflict.Id != entity.Id)
                throw new ConflictException("name_already_exists", $"Já existe um fornecedor com o nome '{name}'.");
        }

        entity.Name = name;
        entity.Email = Clean(req.Email);
        entity.RmaEmail = Clean(req.RmaEmail);
        entity.Phone = Clean(req.Phone);
        entity.Website = Clean(req.Website);
        entity.GarantiaB2BDiasDefault = req.GarantiaB2BDiasDefault is > 0 ? req.GarantiaB2BDiasDefault : null;
        entity.Notas = Clean(req.Notas);
        entity.Active = req.Active;
        entity.RegimeIva = ResolveRegime(req);
        entity.Pais = CleanPais(req.Pais);
        if (req.Nif is not null) entity.Nif = await NifLivreAsync(req.Nif, entity.Id, ct);
        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Update, nameof(Fornecedor), entity.Id, new { entity.Name, entity.Active }, ct: ct);
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.FindByIdAsync(id, ct)
            ?? throw new NotFoundException("Fornecedor", id);
        _repo.Remove(entity);
        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Delete, nameof(Fornecedor), id, new { entity.Name }, ct: ct);
    }

    public async Task<FornecedorDto> FundirAsync(Guid destinoId, Guid origemId, CancellationToken ct = default)
    {
        if (destinoId == origemId)
            throw new ValidationException("fundir_mesmo", "Escolhe dois fornecedores diferentes.");
        var destino = await _repo.FindByIdAsync(destinoId, ct) ?? throw new NotFoundException("Fornecedor", destinoId);
        var origem = await _repo.FindByIdAsync(origemId, ct) ?? throw new NotFoundException("Fornecedor", origemId);
        var chaveDestino = NifFornecedor.Chave(destino.Nif);
        var chaveOrigem = NifFornecedor.Chave(origem.Nif);
        if (chaveDestino is not null && chaveOrigem is not null && chaveDestino != chaveOrigem)
            throw new ConflictException("nif_diferente",
                $"\"{destino.Name}\" ({destino.Nif}) e \"{origem.Name}\" ({origem.Nif}) têm NIFs diferentes — não são o mesmo fornecedor.");

        // O destino fica com o que lhe faltar da origem (NIF, contactos, país, regras aprendidas).
        destino.Nif ??= origem.Nif;
        destino.Email ??= origem.Email;
        destino.RmaEmail ??= origem.RmaEmail;
        destino.Phone ??= origem.Phone;
        destino.Website ??= origem.Website;
        destino.Pais ??= origem.Pais;
        destino.GarantiaB2BDiasDefault ??= origem.GarantiaB2BDiasDefault;
        destino.DefaultDespesaCategoria ??= origem.DefaultDespesaCategoria;
        if (destino.RegimeIva == RegimeIvaFornecedor.Nacional && origem.RegimeIva != RegimeIvaFornecedor.Nacional)
            destino.RegimeIva = origem.RegimeIva;
        if (origem.Notas is not null && destino.Notas?.Contains(origem.Notas) != true)
            destino.Notas = destino.Notas is null ? origem.Notas : $"{destino.Notas}\n{origem.Notas}";
        if (destino.Notas?.Length > 2000) destino.Notas = destino.Notas[..2000];

        var movidos = await _repo.MoverReferenciasAsync(origem, destino, ct);
        origem.Nif = null; // liberta o NIF (o apagado continua na BD com soft-delete)
        _repo.Remove(origem);
        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Update, nameof(Fornecedor), destino.Id,
            new { operation = "fornecedor_fundir", origem = origem.Name, origemId, movidos }, ct: ct);
        return ToDto(destino);
    }

    /// <summary>Sprint 579: NIF normalizado e livre (nenhum outro fornecedor o tem, ignorando o prefixo do país).</summary>
    private async Task<string?> NifLivreAsync(string? nif, Guid? proprioId, CancellationToken ct)
    {
        var n = NifFornecedor.Normalizar(nif);
        if (n is null) return null;
        if (!NifFornecedor.Plausivel(n))
            throw new ValidationException("nif_invalido", "NIF / nº de IVA inválido (ex.: PT500000000, ES B12345678, DE811234567).");
        var chave = NifFornecedor.Chave(n);
        var outro = (await _repo.ListByTenantAsync(includeInactive: true, ct))
            .FirstOrDefault(f => f.Id != proprioId && NifFornecedor.Chave(f.Nif) == chave);
        if (outro is not null)
            throw new ConflictException("nif_ja_existe",
                $"O fornecedor \"{outro.Name}\" já tem este NIF — se é o mesmo, junta os dois em vez de repetir.");
        return n;
    }

    private static string Validate(FornecedorWriteRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            throw new ValidationException("name_required", "Nome obrigatório.");
        var name = req.Name.Trim();
        if (name.Length > 200)
            throw new ValidationException("name_too_long", "Nome até 200 caracteres.");
        if (req.GarantiaB2BDiasDefault is < 0 or > 365 * 5)
            throw new ValidationException("garantia_dias_invalida", "Dias de garantia entre 0 e 1825.");
        return name;
    }

    private static RegimeIvaFornecedor ResolveRegime(FornecedorWriteRequest req)
        => req.RegimeIva is { } r && Enum.IsDefined(r) ? r
            : req.IntraUe ? RegimeIvaFornecedor.UeAutoliquidacao : RegimeIvaFornecedor.Nacional;

    private static string? CleanPais(string? pais)
    {
        var p = Clean(pais)?.ToUpperInvariant();
        if (p is null) return null;
        if (p.Length != 2 || !p.All(char.IsAsciiLetterUpper))
            throw new ValidationException("pais_invalido", "País em código de 2 letras (ex.: PT, NL, FR).");
        return p;
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static FornecedorDto ToDto(Fornecedor f) =>
        new(f.Id, f.Name, f.Code, f.Email, f.RmaEmail, f.Phone, f.Website, f.GarantiaB2BDiasDefault, f.Notas, f.Active, f.CreatedAt, f.IntraUe, f.RegimeIva, f.Pais, f.Nif);
}
