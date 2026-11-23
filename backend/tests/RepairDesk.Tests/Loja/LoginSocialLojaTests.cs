using System.Text.Json;
using FluentAssertions;
using RepairDesk.Services.Loja;

namespace RepairDesk.Tests.Loja;

/// <summary>23/11: entrar na loja com Google/Facebook — só tokens emitidos para a NOSSA app e com email confirmado.</summary>
public class LoginSocialLojaTests
{
    private const string ClientId = "123-abc.apps.googleusercontent.com";
    private static readonly DateTimeOffset Agora = new(2026, 11, 23, 18, 0, 0, TimeSpan.Zero);
    private static JsonElement J(string s) => JsonDocument.Parse(s).RootElement;
    private static string Exp(int minutos) => Agora.AddMinutes(minutos).ToUnixTimeSeconds().ToString();

    [Fact]
    public void Google_TokenValido_DaEmailENome()
    {
        var id = LoginSocialLoja.IdentidadeGoogle(J($$"""{"aud":"{{ClientId}}","iss":"https://accounts.google.com","email":"Ana@Gmail.com","email_verified":"true","name":"Ana Silva","exp":"{{Exp(30)}}"}"""), ClientId, Agora);
        id.Should().Be(new IdentidadeSocial("ana@gmail.com", "Ana Silva"));
    }

    [Theory]
    [InlineData("outra-app.apps.googleusercontent.com", "accounts.google.com", "true", 30)] // token de outra app
    [InlineData(ClientId, "evil.example.com", "true", 30)]                               // emissor errado
    [InlineData(ClientId, "accounts.google.com", "false", 30)]                           // email não confirmado
    [InlineData(ClientId, "accounts.google.com", "true", -5)]                            // expirado
    public void Google_TokenRecusado(string aud, string iss, string verificado, int minutos)
        => LoginSocialLoja.IdentidadeGoogle(J($$"""{"aud":"{{aud}}","iss":"{{iss}}","email":"a@b.pt","email_verified":"{{verificado}}","exp":"{{Exp(minutos)}}"}"""), ClientId, Agora)
            .Should().BeNull();

    [Fact]
    public void Facebook_SoTokenDaNossaApp()
    {
        LoginSocialLoja.TokenDaNossaApp(J("""{"id":"1234567890","name":"LopesTech Loja"}"""), "1234567890").Should().BeTrue();
        LoginSocialLoja.TokenDaNossaApp(J("""{"id":"999","name":"Outra app"}"""), "1234567890").Should().BeFalse("token de outra app");
        LoginSocialLoja.TokenDaNossaApp(J("""{"error":{"message":"x"}}"""), "1234567890").Should().BeFalse();
    }
}
