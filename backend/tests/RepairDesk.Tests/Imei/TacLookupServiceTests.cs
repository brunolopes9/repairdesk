using System.Text;
using FluentAssertions;
using RepairDesk.Services.Imei;

namespace RepairDesk.Tests.Imei;

public class TacLookupServiceTests
{
    private static TacLookupService NewService(out string path)
    {
        path = Path.Combine(Path.GetTempPath(), $"tac-{Guid.NewGuid():N}.json");
        return new TacLookupService(path);
    }

    private static Stream Csv(string content) => new MemoryStream(Encoding.UTF8.GetBytes(content));

    [Fact]
    public async Task Importa_csv_e_resolve_por_imei()
    {
        var svc = NewService(out _);
        var n = await svc.ImportCsvAsync(Csv("tac;brand;model\n35332011;Apple;iPhone 13\n99000123;Samsung;Galaxy A52"));
        n.Should().Be(2);

        // IMEI de 15 dígitos cujo TAC (8 primeiros) = 35332011.
        var r = svc.Resolve("353320110123456");
        r.Found.Should().BeTrue();
        r.Brand.Should().Be("Apple");
        r.Model.Should().Be("iPhone 13");
        r.Tac.Should().Be("35332011");
    }

    [Fact]
    public void Tac_desconhecido_devolve_found_false_gracioso()
    {
        var svc = NewService(out _);
        var r = svc.Resolve("123456789012345");
        r.Found.Should().BeFalse();
        r.Brand.Should().BeNull();
    }

    [Fact]
    public void Imei_invalido_ou_curto_nao_rebenta()
    {
        var svc = NewService(out _);
        svc.Resolve("abc").Found.Should().BeFalse();
        svc.Resolve("").Found.Should().BeFalse();
    }

    [Fact]
    public async Task Persiste_e_recarrega_do_disco()
    {
        var svc = NewService(out var path);
        await svc.ImportCsvAsync(Csv("01234567,Xiaomi,Redmi Note 12"));

        // Nova instância lê o ficheiro persistido.
        var svc2 = new TacLookupService(path);
        svc2.Count.Should().Be(1);
        svc2.Resolve("012345670000009").Model.Should().Be("Redmi Note 12");

        File.Delete(path);
    }
}
