using FluentAssertions;
using RepairDesk.API.HostedServices;

namespace RepairDesk.Tests.Documents;

/// <summary>Sprint 620 (Doc 98 F5): que anexos entram e para que pasta vai cada email da caixa das faturas.</summary>
public class FaturasEmailTests
{
    [Theory]
    [InlineData("application/pdf", "x.bin", true)]
    [InlineData("application/octet-stream", "Fatura FT 2026-123.PDF", true)]
    [InlineData("image/jpeg", "foto.jpg", false)]
    [InlineData("text/html", null, false)]
    public void SoPdfs(string tipo, string? nome, bool ok) => FaturasEmail.EPdf(tipo, nome).Should().Be(ok);

    [Fact]
    public void Destino_SemPdf_Erro_Processada()
    {
        FaturasEmail.Destino(0, 0).Should().Be(FaturasEmail.PastaSemPdf); // ex.: código de confirmação do Gmail
        FaturasEmail.Destino(2, 1).Should().Be(FaturasEmail.PastaErros);
        FaturasEmail.Destino(1, 0).Should().Be(FaturasEmail.PastaProcessadas);
    }

    [Theory]
    [InlineData("mail.lopestech.pt", true)]
    [InlineData("imap.gmail.com", true)]
    [InlineData("127.0.0.1", false)]
    [InlineData("localhost", false)]
    [InlineData("db", false)]
    [InlineData("cache.internal", false)]
    [InlineData("https://mail.lopestech.pt", false)]
    public void Host_SoNomesPublicos(string host, bool ok) => FaturasEmail.HostValido(host).Should().Be(ok);
}
