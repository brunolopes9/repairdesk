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
    public void MobileSentrixFatura_LeNumeroINV_EOPedidoComoReferencia()
    {
        // Texto tal como sai do PdfPig: o hífen do nº vem como glifo privado U+E088.
        const string fatura = """
            Subtotal: €16.42
            Envio € 6.95
            Total geral: € 23.37
            Mobilesentrix
            Invoice Date
            03/07/2026
            Bill To
            CLIENTE TESTE
            Número do pedido: 600240227
            Descrição do produto SKU MPN Preço
            Quantidade
            Fulfilled Subtotal
            Montagem de LCD com Moldura Para Xiaomi Poco F3
            Incell) Preto Noite)
            107082120702 € 16.42 1 1 € 16.42
            Total 1 1
            MobileSentrix Europe | sales@mobilesentrix.eu
            Invoice Number
            INV100110146
            """;
        var r = SupplierPdfParser.Parse(fatura);
        r.OrderId.Should().Be("INV-100110146");
        r.OrderReference.Should().Be("600240227");
        r.DateAdded.Should().Be(new DateTime(2026, 7, 3));
        r.TotalCents.Should().Be(2337);
        r.Confidence.Should().Be(ParseConfidence.High);
        r.Items.Select(i => i.LineTotalCents).Should().Equal(1642, 695);
    }

    [Fact]
    public void SpainSellers_LeQtdColadaAoPreco_ERepartesODesconto()
    {
        // Texto tal como sai do PdfPig (cliente anonimizado): a qtd vem colada ao PVP ("5,402" = 5,40 × 2).
        const string fatura = """
            www.spainsellers.com
            29/09/2026FACTURA : 40747/1 FECHA:
            CLIENTE TESTE
            MICROREPUESTOS S.L.U. CIF: B54848346
            REFERENCIA WEB: 798713
            2000000835150 BATERÍA A1773 PARA AIRPOD 1 Y 2 50MAH 5,402 5,40 10,80
            2000010014811 XIAOMI 13T 13T PRO TAPA TRASERA O TAPA BATERIA NEGRA 13,331 13,33 13,33
            8,74 0,00 8,74
            BASE IMPONIBLE IVA TOTAL FACTURA EUROS
            PORTES   4.25
            DESCUENTOS   -19.64
            """;
        var r = SupplierPdfParser.Parse(fatura);
        r.SupplierName.Should().Be("SpainSellers");
        r.OrderId.Should().Be("40747/1");
        r.OrderReference.Should().Be("798713");
        r.DateAdded.Should().Be(new DateTime(2026, 9, 29));
        r.TotalCents.Should().Be(874);
        r.Confidence.Should().Be(ParseConfidence.High);
        r.Items.Select(i => (i.Quantity, i.LineTotalCents)).Should().Equal((2, 201), (1, 248), (1, 425));
        ParseValidator.DocumentWarnings(fatura).Should().ContainSingle().Which.Should().Contain("19.64");
        IntraUeSuppliers.IsKnownIntraUe("SpainSellers").Should().BeTrue();
    }

    [Fact]
    public void InterMovil_LeCabecalhoJuntoAMorada_EDescricoesEmDuasLinhas()
    {
        const string fatura = """
            Factura
            Inter Movil Levante SL
            PT999999990
            CLIENTE TESTE
            Viseu 007962 1 13/10/2026
            Tarjeta
            22082 Embellecedor y Lente de Camara para Samsung
            Galaxy A16 4G A165 A165F - Negra 1,00 2,52 2,52 2,52
            10616 Lente de Camara Trasera para Xiaomi Redmi Note
            11 4G 2201117TG 2201117TI 2201117TY 1,00 0,99 0,99 0,99
            21,00
            13/10/2026 9,71
            3,51 6,20 9,71
            """;
        var r = SupplierPdfParser.Parse(fatura);
        r.OrderId.Should().Be("007962");
        r.DateAdded.Should().Be(new DateTime(2026, 10, 13));
        r.TotalCents.Should().Be(971);
        r.Confidence.Should().Be(ParseConfidence.High);
        r.Items.Select(i => i.Description).Should().Equal(
            "Embellecedor y Lente de Camara para Samsung Galaxy A16 4G A165 A165F - Negra",
            "Lente de Camara Trasera para Xiaomi Redmi Note 11 4G 2201117TG 2201117TI 2201117TY",
            "Portes");
        IntraUeSuppliers.IsKnownIntraUe(r.SupplierName).Should().BeTrue();
    }

    [Theory]
    [InlineData("4Phones")]
    [InlineData("iPartsTrade")]
    [InlineData("Microwire")]
    [InlineData("Utopya")]
    public void FornecedoresDoExcel_LeemNumeroDataETotal(string fornecedor)
    {
        var (texto, numero, data, total, linhas) = fornecedor switch
        {
            "4Phones" => ("""
                4Phones.nl B.V.
                Invoicenumber: INV-NL-104514 Ordernumber: 4EU165273/SO-NL-113244
                Invoicedate: 28/09/2026 Customerreference:
                Article Description Quantity Price per unit VAT Price excl. VAT
                A00003054 Kingston 64GB Canvas Select Plus
                MicroSD Memory Card
                1 pc € 11.95 0% € 11.95
                2928EEB528 Xiaomi 13T, 13T Pro, Redmi K60 Ultra
                Display And Digitizer With Frame Black
                1 pc € 38.95 0% € 38.95
                4Phones.nl B.V.
                Subtotal € 50.90
                Shipping € 14.90
                Total amount € 65.80
                """, "INV-NL-104514", new DateTime(2026, 9, 28), 6580, 3),
            "iPartsTrade" => ("""
                Address: iPartsTrade B.V.
                Date Added 09/03/2026
                Invoice No. I2026-0034103
                Order ID 322599
                Model Product Name Quantity Price excl. tax Total excl. tax
                BAT1694 For Apple iPhone 14 Battery Run Diagnostics
                Battery OEM 1 €9.95 €9.95
                LCD11768
                Samsung Galaxy S20 Plus Display
                1 €49.95 €49.95
                Total excl. VAT €59.90
                next working day €10.95
                Reverse charge - VAT
                exempt €0.00
                Total incl. VAT €70.85
                """, "I2026-0034103", new DateTime(2026, 9, 3), 7085, 3),
            "Microwire" => ("""
                CLOUDS AND ROOTS - UNIPESSOAL LDA
                Fatura recibo FCTR FCTR2026A/4031 Original
                MWPT #12514
                Data
                12/08/2026
                Artigo Descrição Qtd. Un. Origem Transporte Preço Unitário Desc. Imp. Total
                XMREDMIN11P-
                Xiaomi Redmi Note 11 Pro Ecrã
                1 UN 12,36 0% 23% 15,21
                PORTES GLS Expresso 1 UN 3,19 0% 23% 3,92
                Produtos e Serviços 15,55 €
                Total 19,13 €
                """, "FCTR2026A/4031", new DateTime(2026, 8, 12), 1913, 2),
            _ => ("""
                UTOPYA
                Payment Method Invoice Date Due date Shipping Method
                Credit Card 30/07/2026 30/07/2026 UPS Standard
                918273 Complete Screen iPhone 16 (Soft OLED) (BOLT) 1 29,63€ 29,63€0%*
                Shipping cost UPS Standard 1 6,95€ 6,95€0%*
                Total 36,58€
                Invoice # INV-1024600 - Order # UE027541  - Page 1/1
                """, "INV-1024600", new DateTime(2026, 7, 30), 3658, 2),
        };
        var r = SupplierPdfParser.Parse(texto);
        r.OrderId.Should().Be(numero);
        r.DateAdded.Should().Be(data);
        r.TotalCents.Should().Be(total);
        r.Items.Should().HaveCount(linhas);
        r.Confidence.Should().Be(ParseConfidence.High);
    }

    [Fact]
    public void Tudo4MobileAntigo_SemNumeroNemTotal_JaNaoDizConfiancaAlta()
    {
        var r = SupplierPdfParser.Parse("Tudo4Mobile\nAlguma coisa 5.00€\n");
        r.Confidence.Should().NotBe(ParseConfidence.High);
    }
}
