using RepairDesk.Common.Helpers;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Clientes;

namespace RepairDesk.Services.PriceTable;

public interface IPriceTableService
{
    Task<PagedResult<PriceTableEntryDto>> SearchAsync(string? query, DeviceCategory? cat, string? marca, int page, int pageSize, CancellationToken ct = default);
    Task<PriceTableEntryDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListMarcasAsync(CancellationToken ct = default);
    Task<PriceTableEntryDto> CreateAsync(CreatePriceEntryRequest req, CancellationToken ct = default);
    Task<PriceTableEntryDto> UpdateAsync(Guid id, UpdatePriceEntryRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<ImportPriceTableResponse> ImportCsvAsync(string csv, CancellationToken ct = default);
}

public class PriceTableService : IPriceTableService
{
    private readonly IPriceTableRepository _repo;

    public PriceTableService(IPriceTableRepository repo) => _repo = repo;

    public async Task<PagedResult<PriceTableEntryDto>> SearchAsync(string? query, DeviceCategory? cat, string? marca, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var (items, total) = await _repo.SearchAsync(query, cat, marca, page, pageSize, ct);
        return new PagedResult<PriceTableEntryDto>(items.Select(ToDto).ToList(), page, pageSize, total);
    }

    public async Task<PriceTableEntryDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var e = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("PriceTableEntry", id);
        return ToDto(e);
    }

    public Task<IReadOnlyList<string>> ListMarcasAsync(CancellationToken ct = default) => _repo.ListMarcasAsync(ct);

    public async Task<PriceTableEntryDto> CreateAsync(CreatePriceEntryRequest req, CancellationToken ct = default)
    {
        Validate(req.Marca, req.Modelo, req.Servico, req.PvpCents, req.CustoPecaCents);
        var marca = req.Marca.Trim();
        var modelo = req.Modelo.Trim();
        var servico = req.Servico.Trim();
        if (await _repo.ExistsAsync(marca, modelo, servico, null, ct))
            throw new ConflictException("price_duplicate", $"Já existe entrada para {marca} {modelo} · {servico}.");
        var e = new PriceTableEntry
        {
            Categoria = req.Categoria,
            Marca = marca,
            Modelo = modelo,
            Servico = servico,
            CustoPecaCents = req.CustoPecaCents,
            PvpCents = req.PvpCents,
            TempoEstimadoMin = req.TempoEstimadoMin,
            Notas = req.Notas?.Trim(),
            Activo = true,
        };
        await _repo.AddAsync(e, ct);
        await _repo.SaveAsync(ct);
        return ToDto(e);
    }

    public async Task<PriceTableEntryDto> UpdateAsync(Guid id, UpdatePriceEntryRequest req, CancellationToken ct = default)
    {
        var e = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("PriceTableEntry", id);
        Validate(req.Marca, req.Modelo, req.Servico, req.PvpCents, req.CustoPecaCents);
        var marca = req.Marca.Trim();
        var modelo = req.Modelo.Trim();
        var servico = req.Servico.Trim();
        if (await _repo.ExistsAsync(marca, modelo, servico, id, ct))
            throw new ConflictException("price_duplicate", $"Já existe outra entrada para {marca} {modelo} · {servico}.");
        e.Categoria = req.Categoria;
        e.Marca = marca;
        e.Modelo = modelo;
        e.Servico = servico;
        e.CustoPecaCents = req.CustoPecaCents;
        e.PvpCents = req.PvpCents;
        e.TempoEstimadoMin = req.TempoEstimadoMin;
        e.Notas = string.IsNullOrWhiteSpace(req.Notas) ? null : req.Notas.Trim();
        e.Activo = req.Activo;
        await _repo.SaveAsync(ct);
        return ToDto(e);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var e = await _repo.FindByIdAsync(id, ct) ?? throw new NotFoundException("PriceTableEntry", id);
        _repo.Remove(e);
        await _repo.SaveAsync(ct);
    }

    public async Task<ImportPriceTableResponse> ImportCsvAsync(string csv, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(csv))
            throw new ValidationException("csv_vazio", "CSV vazio.");

        var rows = CsvParser.Parse(csv);
        if (rows.Count < 2)
            throw new ValidationException("csv_sem_dados", "CSV precisa de header + pelo menos 1 linha.");

        var header = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToArray();
        int Idx(params string[] names) =>
            header.Select((h, i) => new { h, i }).FirstOrDefault(x => names.Contains(x.h))?.i ?? -1;

        var iCat = Idx("categoria", "category");
        var iMarca = Idx("marca", "brand");
        var iModelo = Idx("modelo", "model");
        var iServico = Idx("servico", "serviço", "service");
        var iCusto = Idx("custo", "custo_peca", "custopeca");
        var iPvp = Idx("pvp", "preco", "preço", "pvp_medio");
        var iTempo = Idx("tempo", "tempo_min", "tempomin", "tempo_estimado");
        var iNotas = Idx("notas", "notes");

        if (iMarca < 0 || iModelo < 0 || iServico < 0 || iPvp < 0)
            throw new ValidationException("csv_falta_coluna",
                "Header obrigatório: marca, modelo, servico, pvp [+ categoria, custo, tempo, notas opcionais].");

        var erros = new List<PriceImportError>();
        var criadas = 0;
        var ignoradas = 0;

        for (int i = 1; i < rows.Count; i++)
        {
            var linha = i + 1;
            var row = rows[i];
            string? Get(int idx) => idx >= 0 && idx < row.Length ? row[idx].Trim() : null;

            var marca = Get(iMarca);
            var modelo = Get(iModelo);
            var servico = Get(iServico);
            var pvpStr = Get(iPvp);

            if (string.IsNullOrWhiteSpace(marca) || string.IsNullOrWhiteSpace(modelo) || string.IsNullOrWhiteSpace(servico))
            {
                erros.Add(new PriceImportError(linha, "marca/modelo/servico", "Campos obrigatórios em branco.", null));
                continue;
            }

            var pvp = ParseEuros(pvpStr);
            if (pvp is null)
            {
                erros.Add(new PriceImportError(linha, "pvp", "PVP inválido.", pvpStr));
                continue;
            }

            if (await _repo.ExistsAsync(marca, modelo, servico, null, ct))
            {
                ignoradas++;
                continue;
            }

            try
            {
                var entry = new PriceTableEntry
                {
                    Categoria = ParseCategoria(Get(iCat)),
                    Marca = marca,
                    Modelo = modelo,
                    Servico = servico,
                    CustoPecaCents = ParseEuros(Get(iCusto)),
                    PvpCents = pvp.Value,
                    TempoEstimadoMin = int.TryParse(Get(iTempo), out var t) ? t : null,
                    Notas = Get(iNotas),
                    Activo = true,
                };
                await _repo.AddAsync(entry, ct);
                await _repo.SaveAsync(ct);
                criadas++;
            }
            catch (Exception ex)
            {
                erros.Add(new PriceImportError(linha, "?", ex.Message, $"{marca} {modelo}"));
            }
        }

        return new ImportPriceTableResponse(rows.Count - 1, criadas, ignoradas, erros.Count, erros);
    }

    private static void Validate(string? marca, string? modelo, string? servico, int pvp, int? custo)
    {
        if (string.IsNullOrWhiteSpace(marca)) throw new ValidationException("marca_required", "Marca obrigatória.");
        if (string.IsNullOrWhiteSpace(modelo)) throw new ValidationException("modelo_required", "Modelo obrigatório.");
        if (string.IsNullOrWhiteSpace(servico)) throw new ValidationException("servico_required", "Serviço obrigatório.");
        if (pvp < 0) throw new ValidationException("pvp_invalido", "PVP não pode ser negativo.");
        if (custo is < 0) throw new ValidationException("custo_invalido", "Custo não pode ser negativo.");
    }

    private static DeviceCategory ParseCategoria(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return DeviceCategory.Smartphone;
        var n = raw.Trim().ToLowerInvariant();
        if (int.TryParse(n, out var num) && Enum.IsDefined(typeof(DeviceCategory), num)) return (DeviceCategory)num;
        return n switch
        {
            "smartphone" or "telemovel" or "telemóvel" or "phone" => DeviceCategory.Smartphone,
            "tablet" or "ipad" => DeviceCategory.Tablet,
            "laptop" or "portatil" or "portátil" or "macbook" => DeviceCategory.Laptop,
            "desktop" or "pc" or "computador" => DeviceCategory.Desktop,
            "smartwatch" or "relógio" or "relogio" or "watch" => DeviceCategory.Smartwatch,
            "consola" or "console" => DeviceCategory.Consola,
            _ => DeviceCategory.Outro,
        };
    }

    private static int? ParseEuros(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim().Replace("€", "").Replace(" ", "").Replace(" ", "");
        var lastComma = s.LastIndexOf(',');
        var lastDot = s.LastIndexOf('.');
        if (lastComma >= 0 && lastDot >= 0)
        {
            if (lastComma > lastDot) s = s.Replace(".", "").Replace(",", ".");
            else s = s.Replace(",", "");
        }
        else if (lastComma >= 0) s = s.Replace(",", ".");
        if (!decimal.TryParse(s, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var d)) return null;
        return (int)Math.Round(d * 100);
    }

    private static PriceTableEntryDto ToDto(PriceTableEntry e)
    {
        int? margemPct = null;
        if (e.CustoPecaCents.HasValue && e.CustoPecaCents > 0)
        {
            var margem = e.PvpCents - e.CustoPecaCents.Value;
            margemPct = (int)Math.Round((double)margem / e.PvpCents * 100);
        }
        return new PriceTableEntryDto(
            e.Id,
            e.Categoria,
            e.Marca,
            e.Modelo,
            e.Servico,
            e.CustoPecaCents,
            e.PvpCents,
            e.TempoEstimadoMin,
            e.Notas,
            e.Activo,
            margemPct);
    }
}
