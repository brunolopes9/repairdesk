using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.Tests.Auth;

/// <summary>
/// Login por username/email + "Esqueci a palavra-passe" (forgot → email com link → reset).
/// </summary>
public class PasswordResetApiTests : IClassFixture<RepairDeskApiFactory>
{
    private const string NewPassword = "Nova!Pass2026";
    private readonly RepairDeskApiFactory _factory;
    private readonly CapturingEmailSender _emails = new();

    public PasswordResetApiTests(RepairDeskApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient NewClient() => _factory
        .WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IEmailSender>(_emails)))
        .CreateClient();

    private async Task<(Guid Id, string Email)> CreateUserAsync(string? userName = null)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var email = $"u-{Guid.NewGuid():N}@test.local";
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = userName ?? email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Utilizador Teste",
            TenantId = RepairDeskApiFactory.TenantId,
            IsActive = true,
        };
        (await users.CreateAsync(user, RepairDeskApiFactory.AdminPassword)).Succeeded.Should().BeTrue();
        return (user.Id, email);
    }

    private string ExtractToken(string email)
    {
        var message = _emails.Sent.Last(m => m.To == email);
        var match = Regex.Match(message.Text, @"token=([A-Za-z0-9_\-]+)");
        match.Success.Should().BeTrue("o email tem de conter o link com o token");
        return match.Groups[1].Value;
    }

    [Fact]
    public async Task Login_ByUserName_Works()
    {
        var userName = $"bruno{Guid.NewGuid():N}"[..20];
        await CreateUserAsync(userName);

        var resp = await NewClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest(userName.ToUpperInvariant(), RepairDeskApiFactory.AdminPassword));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_LegacyEmailField_StillWorks()
    {
        var resp = await NewClient().PostAsJsonAsync("/api/auth/login",
            new { email = RepairDeskApiFactory.AdminEmail, password = RepairDeskApiFactory.AdminPassword });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgotPassword_UnknownAccount_ReturnsAcceptedWithoutEmail()
    {
        var before = _emails.Sent.Count;

        var resp = await NewClient().PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest("ninguem@test.local"));

        resp.StatusCode.Should().Be(HttpStatusCode.Accepted);
        _emails.Sent.Count.Should().Be(before);
    }

    [Fact]
    public async Task ForgotThenReset_ChangesPassword_AndOldOneStopsWorking()
    {
        var (id, email) = await CreateUserAsync();
        var client = NewClient();

        (await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email)))
            .StatusCode.Should().Be(HttpStatusCode.Accepted);
        var token = ExtractToken(email);

        var reset = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(id, token, NewPassword));
        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, RepairDeskApiFactory.AdminPassword)))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, NewPassword)))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgotPassword_ByUserName_SendsToAccountEmail()
    {
        var userName = $"user{Guid.NewGuid():N}"[..16];
        var (_, email) = await CreateUserAsync(userName);

        await NewClient().PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(userName));

        _emails.Sent.Should().Contain(m => m.To == email);
    }

    [Fact]
    public async Task Reset_TokenCanOnlyBeUsedOnce()
    {
        var (id, email) = await CreateUserAsync();
        var client = NewClient();
        await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email));
        var token = ExtractToken(email);

        (await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest(id, token, NewPassword)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var second = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(id, token, "Outra!Pass2026"));

        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("not-base64-!!")]
    [InlineData("dGVzdA")]
    public async Task Reset_InvalidToken_ReturnsBadRequest(string token)
    {
        var (id, _) = await CreateUserAsync();

        var resp = await NewClient().PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(id, token, NewPassword));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateMe_SetUserName_ThenLoginWithIt()
    {
        var (_, email) = await CreateUserAsync();
        var client = NewClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, RepairDeskApiFactory.AdminPassword));
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var userName = $"novo.{Guid.NewGuid():N}"[..18];

        var update = await client.PutAsJsonAsync("/api/auth/me", new UpdateMeRequest("Teste", null, userName));

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        (await update.Content.ReadFromJsonAsync<UserInfo>())!.UserName.Should().Be(userName);
        (await NewClient().PostAsJsonAsync("/api/auth/login", new LoginRequest(userName, RepairDeskApiFactory.AdminPassword)))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("com@arroba")]
    [InlineData("com espaço")]
    public async Task UpdateMe_InvalidUserName_ReturnsBadRequest(string userName)
    {
        var (_, email) = await CreateUserAsync();
        var client = NewClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, RepairDeskApiFactory.AdminPassword));
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var resp = await client.PutAsJsonAsync("/api/auth/me", new UpdateMeRequest("Teste", null, userName));

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed class CapturingEmailSender : IEmailSender
    {
        private readonly ConcurrentQueue<EmailMessage> _sent = new();
        public IReadOnlyList<EmailMessage> Sent => _sent.ToList();
        public bool IsConfigured => true;

        public Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            _sent.Enqueue(message);
            return Task.CompletedTask;
        }
    }
}
