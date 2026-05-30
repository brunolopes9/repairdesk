using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Devices;

public sealed record DeviceDto(
    Guid Id,
    Guid ClienteId,
    string Tipo,
    string? Marca,
    string? Modelo,
    string? Apelido,
    string? Imei,
    string? Serial,
    string? Cor,
    DateOnly? DataAquisicao,
    DateOnly? GarantiaFabricanteUntil,
    string? Notas,
    bool Arquivado,
    DateTime CreatedAt);

public sealed record CreateDeviceRequest(
    Guid ClienteId,
    string Tipo,
    string? Marca,
    string? Modelo,
    string? Apelido,
    string? Imei,
    string? Serial,
    string? Cor,
    DateOnly? DataAquisicao,
    DateOnly? GarantiaFabricanteUntil,
    string? Notas);

public sealed record UpdateDeviceRequest(
    string Tipo,
    string? Marca,
    string? Modelo,
    string? Apelido,
    string? Imei,
    string? Serial,
    string? Cor,
    DateOnly? DataAquisicao,
    DateOnly? GarantiaFabricanteUntil,
    string? Notas,
    bool Arquivado);

public interface IDeviceService
{
    Task<IReadOnlyList<DeviceDto>> ListByClienteAsync(Guid clienteId, bool incluirArquivados, CancellationToken ct = default);
    Task<DeviceDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<DeviceDto> CreateAsync(CreateDeviceRequest req, CancellationToken ct = default);
    Task<DeviceDto> UpdateAsync(Guid id, UpdateDeviceRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public sealed class DeviceService : IDeviceService
{
    private readonly IDeviceRepository _repo;
    private readonly IClienteRepository _clientes;
    private readonly ITenantContext _tenant;
    private readonly IAuditLogger _audit;
    private readonly ICurrentUser _user;

    public DeviceService(
        IDeviceRepository repo,
        IClienteRepository clientes,
        ITenantContext tenant,
        IAuditLogger audit,
        ICurrentUser user)
    {
        _repo = repo;
        _clientes = clientes;
        _tenant = tenant;
        _audit = audit;
        _user = user;
    }

    public async Task<IReadOnlyList<DeviceDto>> ListByClienteAsync(Guid clienteId, bool incluirArquivados, CancellationToken ct = default)
    {
        var list = await _repo.ListByClienteAsync(clienteId, incluirArquivados, ct);
        return list.Select(ToDto).ToList();
    }

    public async Task<DeviceDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var d = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Device", id);
        return ToDto(d);
    }

    public async Task<DeviceDto> CreateAsync(CreateDeviceRequest req, CancellationToken ct = default)
    {
        var tipo = (req.Tipo ?? "").Trim();
        if (tipo.Length is < 2 or > 60)
            throw new ValidationException("tipo_invalido", "Tipo obrigatório (2 a 60 caracteres).");

        // Validar cliente existe (e pertence ao tenant via filter global).
        _ = await _clientes.FindByIdAsync(req.ClienteId, ct) ?? throw new NotFoundException("Cliente", req.ClienteId);

        var imei = NormalizeImei(req.Imei);
        if (imei is not null && await _repo.ExistsImeiAsync(imei, excludeId: null, ct))
            throw new ValidationException("imei_duplicado", $"Já existe um Device com IMEI {imei}.");

        var d = new Device
        {
            Id = Guid.NewGuid(),
            TenantId = _tenant.TenantId ?? Guid.Empty,
            ClienteId = req.ClienteId,
            Tipo = tipo,
            Marca = TrimOrNull(req.Marca),
            Modelo = TrimOrNull(req.Modelo),
            Apelido = TrimOrNull(req.Apelido),
            Imei = imei,
            Serial = TrimOrNull(req.Serial),
            Cor = TrimOrNull(req.Cor),
            DataAquisicao = req.DataAquisicao,
            GarantiaFabricanteUntil = req.GarantiaFabricanteUntil,
            Notas = TrimOrNull(req.Notas),
            Arquivado = false,
        };
        await _repo.AddAsync(d, ct);
        await _repo.SaveAsync(ct);

        await _audit.LogAsync(AuditAction.Create, "Device", d.Id,
            new { d.ClienteId, d.Tipo, d.Imei }, d.TenantId, _user.UserId, ct);

        return ToDto(d);
    }

    public async Task<DeviceDto> UpdateAsync(Guid id, UpdateDeviceRequest req, CancellationToken ct = default)
    {
        var d = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Device", id);

        var tipo = (req.Tipo ?? "").Trim();
        if (tipo.Length is < 2 or > 60)
            throw new ValidationException("tipo_invalido", "Tipo obrigatório (2 a 60 caracteres).");

        var imei = NormalizeImei(req.Imei);
        if (imei is not null && await _repo.ExistsImeiAsync(imei, excludeId: id, ct))
            throw new ValidationException("imei_duplicado", $"Já existe outro Device com IMEI {imei}.");

        d.Tipo = tipo;
        d.Marca = TrimOrNull(req.Marca);
        d.Modelo = TrimOrNull(req.Modelo);
        d.Apelido = TrimOrNull(req.Apelido);
        d.Imei = imei;
        d.Serial = TrimOrNull(req.Serial);
        d.Cor = TrimOrNull(req.Cor);
        d.DataAquisicao = req.DataAquisicao;
        d.GarantiaFabricanteUntil = req.GarantiaFabricanteUntil;
        d.Notas = TrimOrNull(req.Notas);
        d.Arquivado = req.Arquivado;
        await _repo.SaveAsync(ct);

        await _audit.LogAsync(AuditAction.Update, "Device", d.Id,
            new { d.Tipo, d.Imei, d.Arquivado }, d.TenantId, _user.UserId, ct);

        return ToDto(d);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var d = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Device", id);
        _repo.Remove(d);
        await _repo.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Delete, "Device", id,
            new { d.ClienteId, d.Tipo }, d.TenantId, _user.UserId, ct);
    }

    private static string? TrimOrNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string? NormalizeImei(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : digits;
    }

    private static DeviceDto ToDto(Device d) => new(
        d.Id, d.ClienteId, d.Tipo, d.Marca, d.Modelo, d.Apelido,
        d.Imei, d.Serial, d.Cor, d.DataAquisicao, d.GarantiaFabricanteUntil,
        d.Notas, d.Arquivado, d.CreatedAt);
}
