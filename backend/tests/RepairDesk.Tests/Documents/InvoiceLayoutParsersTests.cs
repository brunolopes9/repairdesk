using FluentAssertions;
using RepairDesk.Services.Documents;

namespace RepairDesk.Tests.Documents;

/// <summary>Sprint 560: leitores determinísticos (texto como sai do PdfPig; cliente anonimizado).</summary>
public class InvoiceLayoutParsersTests
{
    private const string CloudInvoiceFatura = """
        ATCUD:JFK59N3G-73144
        Exmos Srs.
        CLIENTE TESTE
        Rua Teste, 1
        TUDO4MOBILE - Import. Export. Unip.Lda.
        Contribuinte: PT515570664
        Factura-Recibo FRE A01/73144
        Natureza: Factura-Recibo
        Contribuinte
        PT999999990
        Data Vencimento
        2026-07-22
        Data Emissão
        2026-07-22
        Código Total da LinhaIVADescPreço Unitário
        (c/IVA)Unid.QuantidadeDescrição
        TUDO4MOBILE - Import. Export. Unip.Lda. - Sede: Estação Metro Rossio, Lj 3 - 1100-243 Lisboa Portugal
        Powered by © CloudInvoice - https://www.cloudinvoice.net
        23% 19.56 €19.56 €3600198 UNI1.000Touch e lcd with frame sams galaxy
        a52 4g / a52 preto
        23% 14.90 €14.90 €343434 UNI1.000Touch e lcd para iph 11 preto
        23% 5.50 €1.10 €4000732 UNI5.000VIDRO TEMPERADO NORMAL
        23% 5.95 €5.95 €6700934 UNI1.000Portes  (*)
        Os artigos/serviços indicados foram efectuados/colocados à disposição do adquirente na data do documento
        Documento emitido para fins de formação
        Imposto Taxa Valor Sujeito Valor Imposto
        IVA - Taxa Normal 23.00 % 37.33 € 8.58 €
        Total Ilíquido (c/IVA) 45.91 €
        TOTAL DOCUMENTO 45.91 €
        """;

    private const string MobileSentrixResumo = """
        Subtotal: €6.98
        Envio €6.95
        VAT 0.00% €0.00
        Total Geral: €13.93
        VAT exempt – intra-Community supply, Article 1381
        Mobilesentrix
        De Keten 4
        Data do Pedido
        22/09/2026
        Bill To
        CLIENTE TESTE
        Descrição do Produto SKU MPN Preço unitário Quantidade
        Encomendado Subtotal
        Carregamento Sem Fios NFC Flex Para Samsung
        Galaxy S8 Plus 107082011558 €0.86 1 €0.86
        Porto de Carregamento com Cabo Flexível para
        Samsung Galaxy S8 Plus G955F Versão
        Internacional)
        107082011612 €4.23 1 €4.23
        Vidro Temperado Casper Pro para iPhone 12 / 12 Pro
        Pacote de Varejo) Transparente) 107082111645 €1.89 1 €1.89
        Total 3
        MobileSentrix Europe | VAT NL866286081B01 | sales@mobilesentrix.eu
        Order Number
        600254790
        """;

    [Fact]
    public void CloudInvoice_LeNumeroDataTotalLinhasEPortes()
    {
        var r = SupplierPdfParser.Parse(CloudInvoiceFatura);

        r.SupplierName.Should().Be("Tudo4Mobile");
        r.OrderId.Should().Be("FRE A01/73144");
        r.DateAdded.Should().Be(new DateTime(2026, 7, 22));
        r.TotalCents.Should().Be(4591);
        r.Confidence.Should().Be(ParseConfidence.High);
        r.Items.Select(i => (i.Description, i.Quantity, i.LineTotalCents)).Should().Equal(
            ("Touch e lcd with frame sams galaxy a52 4g / a52 preto", 1, 1956),
            ("Touch e lcd para iph 11 preto", 1, 1490),
            ("VIDRO TEMPERADO NORMAL", 5, 550),
            ("Portes", 1, 595));
        SupplierInvoiceImportService.ClassifyItemDescription("Portes").Should().Be(SupplierItemKind.Shipping);
        ParseValidator.DocumentWarnings(CloudInvoiceFatura).Should().ContainSingle().Which.Should().Contain("formação");
    }

    [Fact]
    public void CloudInvoice_QueNaoReconcilia_FicaComConfiancaBaixa()
    {
        var r = SupplierPdfParser.Parse(CloudInvoiceFatura.Replace("TOTAL DOCUMENTO 45.91 €", "TOTAL DOCUMENTO 99.00 €"));
        r.Confidence.Should().Be(ParseConfidence.Low);
    }

    [Fact]
    public void CloudInvoice_NotaDeCredito_NaoEConfiancaAlta()
    {
        var r = SupplierPdfParser.Parse(CloudInvoiceFatura.Replace("Factura-Recibo FRE A01/73144", "Nota de Crédito NC A01/120"));
        r.OrderId.Should().Be("NC A01/120");
        r.Confidence.Should().Be(ParseConfidence.Low);
    }

    [Fact]
    public void MobileSentrix_LeEncomendaDataTotalLinhasMultilinhaEEnvio()
    {
        var r = SupplierPdfParser.Parse(MobileSentrixResumo);

        r.SupplierName.Should().Be("MobileSentrix");
        r.OrderId.Should().Be("600254790");
        r.DateAdded.Should().Be(new DateTime(2026, 9, 22));
        r.TotalCents.Should().Be(1393);
        r.Confidence.Should().Be(ParseConfidence.High);
        r.Items.Should().HaveCount(4);
        r.Items[0].Description.Should().Be("Carregamento Sem Fios NFC Flex Para Samsung Galaxy S8 Plus");
        r.Items[1].Description.Should().StartWith("Porto de Carregamento").And.EndWith("Internacional)");
        r.Items[1].LineTotalCents.Should().Be(423);
        r.Items[3].Should().Be(new SupplierPdfItem("Envio (portes)", 1, 695));
    }

    [Fact]
    public void Tudo4MobileAntigo_SemNumeroNemTotal_JaNaoDizConfiancaAlta()
    {
        var r = SupplierPdfParser.Parse("Tudo4Mobile\nAlguma coisa 5.00€\n");
        r.Confidence.Should().NotBe(ParseConfidence.High);
    }
}
