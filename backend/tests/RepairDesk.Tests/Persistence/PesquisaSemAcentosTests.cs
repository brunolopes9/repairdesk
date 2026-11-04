using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.DAL.Persistence;

namespace RepairDesk.Tests.Persistence;

/// <summary>Sprint 591: "pelicula" encontra "Película" — a pesquisa não olha a acentos nem maiúsculas.</summary>
public class PesquisaSemAcentosTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    private static AppDbContext Ctx(Action<DbContextOptionsBuilder<AppDbContext>> provider)
    {
        var b = new DbContextOptionsBuilder<AppDbContext>();
        provider(b);
        return new AppDbContext(b.Options, new Tenantz(Tenant));
    }

    [Theory]
    [InlineData("pelicula", true)]
    [InlineData("PELICULA GEL", true)]
    [InlineData("hidrogel", true)]
    [InlineData("ecra", false)]
    [InlineData("100%", false)]
    public async Task Artigos_PesquisaSemAcentos(string q, bool encontra)
    {
        var db = Ctx(b => b.UseInMemoryDatabase($"rd-{Guid.NewGuid():N}"));
        db.Artigos.Add(new Artigo { TenantId = Tenant, Nome = "Película de gel", NomeFatura = "Película de hidrogel", Sku = "LT-1", CodigoBarras = "200001" });
        await db.SaveChangesAsync();

        var r = await new ArtigoRepository(db).ListAsync(q, incluirInativos: false);

        r.Should().HaveCount(encontra ? 1 : 0);
    }

    [Fact]
    public void NoSqlServer_UsaCollationSemAcentos()
    {
        var db = Ctx(b => b.UseSqlServer("Server=.;Database=x;Trusted_Connection=True"));
        var t = PesquisaTexto.Padrao("pelicula");

        var sql = db.Artigos.Where(a => PesquisaTexto.Contem(a.Nome, t)).ToQueryString();

        sql.Should().Contain("COLLATE Latin1_General_100_CI_AI LIKE");
    }

    [Fact]
    public void Padrao_TrataCaracteresEspeciaisComoTexto()
    {
        PesquisaTexto.Padrao("10%_[x]").Should().Be("%10[%][_][[]x]%");
        PesquisaTexto.Contem("Desconto 10% já", PesquisaTexto.Padrao("10%")).Should().BeTrue();
        PesquisaTexto.Contem("Ecrã iPhone", PesquisaTexto.Padrao("ECRA")).Should().BeTrue();
    }

    private sealed class Tenantz(Guid id) : ITenantContext
    {
        public Guid? TenantId { get; } = id;
        public bool HasTenant => true;
    }
}
