using FluentAssertions;
using RepairDesk.Services.Moloni;
using Xunit;

namespace RepairDesk.Tests.Moloni;

/// <summary>26/11: símbolos/emojis saíam como ▯ no PDF do Moloni; o limite de artigos do plano não pode travar a fatura.</summary>
public class MoloniTextoFaturaTests
{
    [Theory]
    [InlineData("Serviço — Ecrã Xiaomi 13 Lite / Civi 2 📱 ⚫ preto️ (recondicionado)", "Serviço — Ecrã Xiaomi 13 Lite / Civi 2 preto (recondicionado)")]
    [InlineData("Película de gel (hidrogel HD)", "Película de gel (hidrogel HD)")]
    [InlineData("Capa\nflip ★ A14", "Capa flip A14")]
    public void TextoFatura_tira_simbolos_e_mantem_acentos(string entrada, string esperado)
        => MoloniService.TextoFatura(entrada, 200).Should().Be(esperado);

    [Fact]
    public void TextoFatura_corta_no_maximo()
        => MoloniService.TextoFatura(new string('a', 250), 200).Should().HaveLength(200);

    [Fact]
    public void LimiteDoPlano_reconhece_a_mensagem_do_Moloni()
    {
        MoloniService.LimiteDoPlano(new MoloniException("Number of items is over the allowed limit.")).Should().BeTrue();
        MoloniService.LimiteDoPlano(new MoloniException("Expired subscription.")).Should().BeFalse();
    }
}
