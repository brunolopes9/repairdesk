using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using RepairDesk.Services.Documents;

namespace RepairDesk.Services.Vendas;

/// <summary>"criada" (venda nova) | "ligada" (a venda já existia — ficou com o PDF) | "repetida" (já tinha esta fatura).</summary>
public sealed record VendaFaturaImportResult(Guid VendaId, int VendaNumero, string InvoiceNumber, string Resultado, string? Aviso);

public interface IVendaFaturaService
{
    Task<VendaFaturaImportResult> ImportarAsync(byte[] pdf, CancellationToken ct = default);
    Task<(byte[] Pdf, string Nome)> PdfAsync(Guid vendaId, CancellationToken ct = default);
    Task<(byte[] Zip, string Filename)> ExportZipAsync(DateTime from, DateTime to, CancellationToken ct = default);
}

/// <summary>
/// Sprint 565: faturas de venda emitidas no Moloni entram no Mender pelo PDF. O PDF é lido (nº, data,
/// cliente, linhas e IVA) e dá origem a uma venda entregue com a fatura registada — ou, se a venda já
/// existir com esse nº de fatura, fica-lhe só anexado. Idempotente: a mesma fatura duas vezes não duplica.
/// As linhas não mexem no stock (são serviços/artigos da fatura); vendas de stock lançam-se no editor.
/// </summary>
public class VendaFaturaService : IVendaFaturaService
{
    private const string KeyPrefix = "sales-invoices/";

    private readonly IVendaService _vendaService;
    private readonly IVendaRepository _vendas;
    private readonly IClienteRepository _clientes;
    private readonly IPhotoStorage _storage;
    private readonly ITenantContext _tenant;
    private readonly IAuditLogger _audit;

    public VendaFaturaService(IVendaService vendaService, IVendaRepository vendas, IClienteRepository clientes,
        IPhotoStorage storage, ITenantContext tenant, IAuditLogger audit)
    {
        _vendaService = vendaService;
        _vendas = vendas;
        _clientes = clientes;
        _storage = storage;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task<VendaFaturaImportResult> ImportarAsync(byte[] pdf, CancellationToken ct = default)
    {
        string texto;
        using (var ms = new MemoryStream(pdf)) texto = PdfTextExtractor.Extract(ms, "fatura.pdf").Text ?? string.Empty;
        if (!MoloniFaturaParser.Matches(texto))
            throw new ValidationException("nao_e_fatura_moloni", "Este PDF não parece uma fatura emitida no Moloni.");
        var lida = MoloniFaturaParser.Parse(texto)
            ?? throw new ValidationException("fatura_ilegivel", "Não consegui ler o nº, a data, o cliente e as linhas desta fatura.");
        return await ImportarLidaAsync(lida, pdf, ct);
    }

    /// <summary>Cria/liga a venda a partir de uma fatura já lida (separado da leitura do PDF para os testes).</summary>
    public async Task<VendaFaturaImportResult> ImportarLidaAsync(FaturaVendaLida lida, byte[] pdf, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not { } tenantId)
            throw new ForbiddenException("tenant_required", "Sem tenant no contexto.");
        var sha = Convert.ToHexString(SHA256.HashData(pdf)).ToLowerInvariant();

        var existente = await _vendas.FindByInvoiceNumberAsync(lida.Numero, ct);
        if (existente is not null)
        {
            if (existente.FaturaPdfPath is not null)
                return new(existente.Id, existente.Numero, lida.Numero, "repetida",
                    existente.FaturaPdfSha256 == sha ? null : "A venda já tinha um PDF desta fatura — mantive o primeiro.");
            await GuardarPdfAsync(existente, tenantId, lida, pdf, sha, ct);
            await _vendas.SaveAsync(ct);
            await _audit.LogAsync(AuditAction.Update, nameof(Venda), existente.Id, new { operation = "anexar_fatura_pdf", lida.Numero }, ct: ct);
            var aviso = existente.TotalCents != lida.TotalCents
                ? $"O total da venda ({existente.TotalCents / 100m:0.00} €) é diferente do da fatura ({lida.TotalCents / 100m:0.00} €)."
                : null;
            return new(existente.Id, existente.Numero, lida.Numero, "ligada", aviso);
        }

        var cliente = await ClienteAsync(tenantId, lida, ct);
        var (linhas, avisoTotal) = Linhas(lida);
        var tipo = lida.Numero.Contains("REPARA", StringComparison.OrdinalIgnoreCase) && cliente is not null
            ? VendaTipo.Reparacao : VendaTipo.Servico;
        var notas = $"Importada da fatura {lida.Numero} (PDF do Moloni)." + (lida.MotivoIsencao is { } m ? $" Isenção: {m}." : string.Empty);

        var criada = await _vendaService.CreateAsync(new VendaWriteRequest(
            tipo, cliente?.Id, null, null, notas, linhas, VendaEstado.Entregue, PaymentMethod.Outro), ct);
        await _vendaService.RegistarFaturaAsync(criada.Id, new RegistarFaturaRequest(lida.Numero, lida.Data), ct);

        var venda = await _vendas.FindByIdAsync(criada.Id, ct) ?? throw new NotFoundException("Venda", criada.Id);
        await GuardarPdfAsync(venda, tenantId, lida, pdf, sha, ct);
        await _vendas.SaveAsync(ct);
        await _audit.LogAsync(AuditAction.Create, nameof(Venda), venda.Id, new { operation = "importar_fatura_venda", lida.Numero, lida.TotalCents }, ct: ct);
        return new(venda.Id, venda.Numero, lida.Numero, "criada", avisoTotal);
    }

    public async Task<(byte[] Pdf, string Nome)> PdfAsync(Guid vendaId, CancellationToken ct = default)
    {
        var venda = await _vendas.FindByIdAsync(vendaId, ct) ?? throw new NotFoundException("Venda", vendaId);
        if (venda.FaturaPdfPath is null) throw new NotFoundException("FaturaVendaPdf", vendaId);
        await using var s = await _storage.DownloadAsync(KeyPrefix + venda.FaturaPdfPath, ct);
        using var ms = new MemoryStream();
        await s.CopyToAsync(ms, ct);
        return (ms.ToArray(), NomeSeguro(venda.InvoiceNumber ?? $"venda-{venda.Numero}") + ".pdf");
    }

    public async Task<(byte[] Zip, string Filename)> ExportZipAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        if (to < from) throw new ValidationException("invalid_range", "A data final tem de ser igual ou depois da inicial.");
        if ((to - from).TotalDays > 400) throw new ValidationException("range_too_large", "Período máximo 400 dias.");
        var vendas = await _vendas.ListComFaturaPdfAsync(from.Date, to.Date.AddDays(1), ct);

        var usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var indice = new StringBuilder("ficheiro;cliente;nif;documento;data;total;iva\r\n");
        var pt = CultureInfo.GetCultureInfo("pt-PT");
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var v in vendas)
            {
                var data = v.InvoiceEmittedAt ?? v.Data;
                var cliente = v.Cliente?.Nome ?? "Consumidor final";
                var baseNome = $"{data:yyyy-MM}/{NomeSeguro(cliente)}/{data:yyyy-MM-dd} {NomeSeguro(v.InvoiceNumber ?? $"venda {v.Numero}")}";
                var nome = baseNome + ".pdf";
                for (var i = 2; !usados.Add(nome); i++) nome = $"{baseNome} ({i}).pdf";
                try
                {
                    await using var s = await _storage.DownloadAsync(KeyPrefix + v.FaturaPdfPath, ct);
                    await using var e = zip.CreateEntry(nome, CompressionLevel.Fastest).Open();
                    await s.CopyToAsync(e, ct);
                }
                catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
                {
                    nome = "(PDF em falta) " + nome;
                }
                indice.Append(string.Join(';', Csv(nome), Csv(cliente), Csv(v.Cliente?.Nif), Csv(v.InvoiceNumber),
                    data.ToString("yyyy-MM-dd"), (v.TotalCents / 100m).ToString("0.00", pt), (v.IvaCents / 100m).ToString("0.00", pt))).Append("\r\n");
            }
            await using var w = new StreamWriter(zip.CreateEntry("indice.csv").Open(), new UTF8Encoding(true));
            await w.WriteAsync(indice.ToString());
        }
        return (ms.ToArray(), $"Faturas-venda_{from:yyyy-MM-dd}_a_{to:yyyy-MM-dd}.zip");
    }

    // ---------- regras ----------

    /// <summary>Por NIF (o mesmo cliente não se duplica); sem NIF e não consumidor final → cliente novo só com o nome.</summary>
    private async Task<Cliente?> ClienteAsync(Guid tenantId, FaturaVendaLida lida, CancellationToken ct)
    {
        if (lida.ConsumidorFinal) return null;
        if (lida.ClienteNif is { } nif && await _clientes.FindByNifAsync(nif, ct) is { } existente) return existente;
        var novo = new Cliente
        {
            TenantId = tenantId,
            Nome = lida.ClienteNome.Length > 200 ? lida.ClienteNome[..200] : lida.ClienteNome,
            Nif = lida.ClienteNif is { Length: <= 20 } n ? n : null,
            Morada = lida.ClienteMorada is { } m ? (m.Length > 200 ? m[..200] : m) : null,
            Notas = $"Criado ao importar a fatura {lida.Numero}.",
        };
        await _clientes.AddAsync(novo, ct);
        await _clientes.SaveAsync(ct);
        return novo;
    }

    /// <summary>
    /// A venda guarda preços COM IVA; a fatura imprime-os SEM IVA. Linhas iguais juntam-se e, quando o
    /// preço c/ IVA não dá cêntimos certos (29,50 × 1,23 = 36,285), ficam numa linha de 1 com o total —
    /// assim o total e o IVA batem com a fatura (12 × 29,50 € → 435,42 €, IVA 81,42 €).
    /// </summary>
    private static (List<VendaLinhaWriteRequest> Linhas, string? Aviso) Linhas(FaturaVendaLida lida)
    {
        var linhas = new List<VendaLinhaWriteRequest>();
        foreach (var g in lida.Linhas.GroupBy(l => (l.Descricao, l.PrecoUnitarioSemIvaCents, l.IvaRate)))
        {
            var qtd = g.Sum(l => l.Quantidade);
            var fator = 1m + g.Key.IvaRate / 100m;
            var unitComIva = g.Key.PrecoUnitarioSemIvaCents * fator;
            if (qtd == decimal.Truncate(qtd) && unitComIva == decimal.Truncate(unitComIva))
            {
                linhas.Add(new VendaLinhaWriteRequest(null, null, g.Key.Descricao, (int)qtd, (int)unitComIva, 0, g.Key.IvaRate));
                continue;
            }
            var totalBase = g.Key.PrecoUnitarioSemIvaCents * qtd;
            var desc = $"{g.Key.Descricao} ({qtd.ToString("0.##", CultureInfo.GetCultureInfo("pt-PT"))} × {g.Key.PrecoUnitarioSemIvaCents / 100m:0.00} €)";
            linhas.Add(new VendaLinhaWriteRequest(null, null, desc.Length > 300 ? desc[..300] : desc, 1,
                (int)Math.Round(totalBase * fator, MidpointRounding.AwayFromZero), 0, g.Key.IvaRate));
        }

        var diferenca = lida.TotalCents - linhas.Sum(l => l.PrecoUnitarioCents * l.Quantidade);
        if (diferenca == 0) return (linhas, null);
        var ajustavel = linhas.Where(l => l.Quantidade == 1).OrderByDescending(l => l.PrecoUnitarioCents).FirstOrDefault();
        if (Math.Abs(diferenca) <= 5 && ajustavel is not null)
        {
            linhas[linhas.IndexOf(ajustavel)] = ajustavel with { PrecoUnitarioCents = ajustavel.PrecoUnitarioCents + diferenca };
            return (linhas, null);
        }
        return (linhas, $"O total calculado difere {diferenca / 100m:0.00} € do total da fatura — confirma a venda.");
    }

    private async Task GuardarPdfAsync(Venda venda, Guid tenantId, FaturaVendaLida lida, byte[] pdf, string sha, CancellationToken ct)
    {
        var rel = $"{tenantId}/{lida.Data:yyyy}/{lida.Data:MM}/{NomeSeguro(lida.Numero)}.pdf";
        using var ms = new MemoryStream(pdf);
        await _storage.UploadAsync(KeyPrefix + rel, ms, "application/pdf", ct);
        venda.FaturaPdfPath = rel;
        venda.FaturaPdfSha256 = sha;
    }

    private static string NomeSeguro(string s)
    {
        var invalidos = Path.GetInvalidFileNameChars();
        var limpo = new string(s.Trim().Select(c => c is '/' or '\\' || invalidos.Contains(c) ? '-' : c).ToArray());
        return limpo.Length > 80 ? limpo[..80].TrimEnd() : limpo;
    }

    private static string Csv(string? v) => v is null ? "" : v.Contains(';') || v.Contains('"') ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
}
