using RepairDesk.Services.Conteudo.Redes;
using Xunit;

namespace RepairDesk.Tests.Conteudo;

/// <summary>Os erros das redes nunca podem levar segredos para o ecrã ou para os logs.</summary>
public class RedesSegredosTests
{
    [Fact]
    public void Erro_da_Threads_com_client_secret_em_claro_fica_tapado()
    {
        var form = new Dictionary<string, string?> { ["client_id"] = "1785496849329380", ["client_secret"] = "abc123def456", ["code"] = "AQBxyz-987654" };
        var msg = MetaGraphClient.SemSegredos("Invalid client_secret: abc123def456", MetaGraphClient.Segredos(form));

        Assert.DoesNotContain("abc123def456", msg);
        Assert.Contains("client_secret", msg);
        Assert.DoesNotContain("1785496849329380", MetaGraphClient.Segredos(form));
    }

    [Fact]
    public void Segredo_que_nao_foi_enviado_mas_vem_no_formato_conhecido_tambem_fica_tapado()
    {
        var msg = MetaGraphClient.SemSegredos("Bad request: access_token=EAAG1234567890xyz expired", []);
        Assert.DoesNotContain("EAAG1234567890xyz", msg);
    }
}
