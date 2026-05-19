using FluentValidation;
using RepairDesk.Common.Helpers;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Clientes;

public interface IClienteService
{
    Task<PagedResult<ClienteDto>> SearchAsync(string? query, int page, int pageSize, CancellationToken ct = default);
    Task<ClienteDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<ClienteDto> CreateAsync(CreateClienteRequest req, CancellationToken ct = default);
    Task<ClienteDto> UpdateAsync(Guid id, UpdateClienteRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<ImportClientesResponse> ImportCsvAsync(string csv, CancellationToken ct = default);
    Task<byte[]> ExportCsvAsync(CancellationToken ct = default);
    Task<LookupOrCreateClienteResponse> LookupOrCreateAsync(CreateClienteRequest req, CancellationToken ct = default);
}

public sealed record LookupOrCreateClienteResponse(ClienteDto Cliente, bool Created);

public class ClienteService : IClienteService
{
    private readonly IClienteRepository _repo;
    private readonly IValidator<CreateClienteRequest> _createValidator;
    private readonly IValidator<UpdateClienteRequest> _updateValidator;

    public ClienteService(
        IClienteRepository repo,
        IValidator<CreateClienteRequest> createValidator,
        IValidator<UpdateClienteRequest> updateValidator)
    {
        _repo = repo;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<PagedResult<ClienteDto>> SearchAsync(string? query, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, total) = await _repo.SearchAsync(query, page, pageSize, ct);
        return new PagedResult<ClienteDto>(items.Select(ToDto).ToList(), page, pageSize, total);
    }

    public async Task<ClienteDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var cliente = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Cliente", id);
        return ToDto(cliente);
    }

    public async Task<ClienteDto> CreateAsync(CreateClienteRequest req, CancellationToken ct = default)
    {
        await _createValidator.ValidateAndThrowAsync(req, ct);
        if (!string.IsNullOrWhiteSpace(req.Nif) && await _repo.NifExistsAsync(req.Nif, null, ct))
            throw new ConflictException("nif_in_use", "Já existe um cliente com esse NIF.");

        var cliente = new Cliente
        {
            Nome = req.Nome.Trim(),
            Telefone = string.IsNullOrWhiteSpace(req.Telefone) ? null : NormalizePhone(req.Telefone),
            Email = req.Email?.Trim(),
            Nif = req.Nif?.Trim(),
            Notas = req.Notas?.Trim()
        };
        await _repo.AddAsync(cliente, ct);
        await _repo.SaveAsync(ct);
        return ToDto(cliente);
    }

    public async Task<ClienteDto> UpdateAsync(Guid id, UpdateClienteRequest req, CancellationToken ct = default)
    {
        await _updateValidator.ValidateAndThrowAsync(req, ct);
        var cliente = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Cliente", id);
        if (!string.IsNullOrWhiteSpace(req.Nif) && await _repo.NifExistsAsync(req.Nif, id, ct))
            throw new ConflictException("nif_in_use", "Já existe um cliente com esse NIF.");

        cliente.Nome = req.Nome.Trim();
        cliente.Telefone = string.IsNullOrWhiteSpace(req.Telefone) ? null : NormalizePhone(req.Telefone);
        cliente.Email = req.Email?.Trim();
        cliente.Nif = req.Nif?.Trim();
        cliente.Notas = req.Notas?.Trim();
        await _repo.SaveAsync(ct);
        return ToDto(cliente);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var cliente = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("Cliente", id);
        _repo.Remove(cliente);
        await _repo.SaveAsync(ct);
    }

    /// <summary>
    /// Find-by-NIF-or-create atómico. Pensado para integração externa (loja online,
    /// importadores) que recebem dados de cliente e precisam de garantir que existe
    /// no RepairDesk sem causar duplicados. Sprint 69.
    /// </summary>
    public async Task<LookupOrCreateClienteResponse> LookupOrCreateAsync(CreateClienteRequest req, CancellationToken ct = default)
    {
        // Se tem NIF, tenta primeiro lookup. Sem NIF, vamos sempre criar (não há
        // ID estável para deduplicar).
        if (!string.IsNullOrWhiteSpace(req.Nif))
        {
            var existing = await _repo.FindByNifAsync(req.Nif.Trim(), ct);
            if (existing is not null) return new LookupOrCreateClienteResponse(ToDto(existing), Created: false);
        }

        var created = await CreateAsync(req, ct);
        return new LookupOrCreateClienteResponse(created, Created: true);
    }

    public async Task<ImportClientesResponse> ImportCsvAsync(string csv, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(csv))
            throw new RepairDesk.Core.Exceptions.ValidationException("csv_vazio", "CSV vazio.");

        var rows = CsvParser.Parse(csv);
        if (rows.Count < 2)
            throw new RepairDesk.Core.Exceptions.ValidationException("csv_sem_dados", "CSV precisa de header + pelo menos 1 linha de dados.");

        var header = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToArray();
        var idxNome = Array.IndexOf(header, "nome");
        var idxTelefone = Array.IndexOf(header, "telefone");
        var idxEmail = Array.IndexOf(header, "email");
        var idxNif = Array.IndexOf(header, "nif");
        var idxNotas = Array.IndexOf(header, "notas");
        if (idxNome < 0)
            throw new RepairDesk.Core.Exceptions.ValidationException("csv_falta_coluna", "Coluna obrigatória 'nome' não encontrada no header. Aceito: nome,telefone,email,nif,notas");

        var erros = new List<ImportError>();
        var criados = new List<ClienteDto>();
        var ignorados = 0;
        for (int i = 1; i < rows.Count; i++)
        {
            var linha = i + 1; // 1-based para humanos
            var row = rows[i];
            string? Get(int idx) => idx >= 0 && idx < row.Length ? row[idx].Trim() : null;

            var nome = Get(idxNome);
            if (string.IsNullOrWhiteSpace(nome))
            {
                erros.Add(new ImportError(linha, "nome", "Nome em branco — linha ignorada.", null));
                continue;
            }

            var telefone = Get(idxTelefone);
            var email = Get(idxEmail);
            var nif = Get(idxNif);
            var notas = Get(idxNotas);

            // Dedupe por NIF: se já existe, ignorar (não duplicar)
            if (!string.IsNullOrWhiteSpace(nif) && await _repo.NifExistsAsync(nif, null, ct))
            {
                ignorados++;
                continue;
            }

            try
            {
                var req = new CreateClienteRequest(nome, telefone, email, nif, notas);
                await _createValidator.ValidateAndThrowAsync(req, ct);
                var cliente = new Cliente
                {
                    Nome = nome,
                    Telefone = string.IsNullOrWhiteSpace(telefone) ? null : NormalizePhone(telefone),
                    Email = string.IsNullOrWhiteSpace(email) ? null : email,
                    Nif = string.IsNullOrWhiteSpace(nif) ? null : nif,
                    Notas = string.IsNullOrWhiteSpace(notas) ? null : notas,
                };
                await _repo.AddAsync(cliente, ct);
                await _repo.SaveAsync(ct);
                criados.Add(ToDto(cliente));
            }
            catch (FluentValidation.ValidationException vex)
            {
                var first = vex.Errors.FirstOrDefault();
                erros.Add(new ImportError(linha, first?.PropertyName ?? "?", first?.ErrorMessage ?? vex.Message, nome));
            }
            catch (Exception ex)
            {
                erros.Add(new ImportError(linha, "?", ex.Message, nome));
            }
        }

        return new ImportClientesResponse(
            TotalLinhas: rows.Count - 1,
            Criados: criados.Count,
            Ignorados: ignorados,
            ComErro: erros.Count,
            ClientesCriados: criados,
            Erros: erros);
    }

    public async Task<byte[]> ExportCsvAsync(CancellationToken ct = default)
    {
        var rows = await _repo.ExportAllAsync(ct);
        var csv = new CsvBuilder();
        csv.Row("nome", "telefone", "email", "nif", "notas", "criadoem");
        foreach (var c in rows)
        {
            csv.Row(c.Nome, c.Telefone, c.Email, c.Nif, c.Notas, c.CreatedAt);
        }
        return csv.ToUtf8WithBom();
    }

    private static string NormalizePhone(string raw) =>
        new(raw.Where(c => !char.IsWhiteSpace(c)).ToArray());

    private static ClienteDto ToDto(Cliente c) =>
        new(c.Id, c.Nome, c.Telefone, c.Email, c.Nif, c.Notas, c.CreatedAt, c.UpdatedAt);
}
