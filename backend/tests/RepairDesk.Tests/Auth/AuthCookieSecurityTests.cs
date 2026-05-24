using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RepairDesk.API.Controllers;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;

namespace RepairDesk.Tests.Auth;

public class AuthCookieSecurityTests
{
    [Fact]
    public async Task Login_InProductionHttps_SetsRefreshCookieStrictSecureHttpOnly()
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            TenantId = RepairDeskApiFactory.TenantId,
            Email = "cookie@test.local",
            UserName = "cookie@test.local",
            DisplayName = "Cookie Test",
            IsActive = true
        };

        var users = MockUserManager();
        users.Setup(x => x.FindByEmailAsync(user.Email)).ReturnsAsync(user);
        users.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(false);
        users.Setup(x => x.CheckPasswordAsync(user, "Test!Pass2026")).ReturnsAsync(true);
        users.Setup(x => x.ResetAccessFailedCountAsync(user)).ReturnsAsync(IdentityResult.Success);
        users.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);
        users.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new[] { "Admin" });

        var refresh = new Mock<IRefreshTokenService>();
        refresh.Setup(x => x.IssueAsync(user, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(("refresh-token", new RefreshToken
            {
                UserId = user.Id,
                TenantId = user.TenantId,
                TokenHash = "hash",
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            }));

        var tokens = new Mock<ITokenService>();
        tokens.SetupGet(x => x.AccessTokenExpiry).Returns(DateTime.UtcNow.AddMinutes(15));
        tokens.Setup(x => x.IssueAccessToken(user, It.IsAny<IEnumerable<string>>())).Returns("access-token");

        var audit = new Mock<IAuditLogger>();
        audit.Setup(x => x.LogAsync(
                It.IsAny<RepairDesk.Core.Enums.AuditAction>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(x => x.EnvironmentName).Returns("Production");

        var http = new DefaultHttpContext();
        http.Request.Scheme = "https";
        var controller = new AuthController(
            users.Object,
            tokens.Object,
            refresh.Object,
            audit.Object,
            env.Object,
            NullLogger<AuthController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };

        var response = await controller.Login(new LoginRequest(user.Email, "Test!Pass2026"), CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>();
        var cookie = http.Response.Headers.SetCookie.ToString();
        cookie.Should().Contain("rd_refresh=refresh-token");
        cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase).Should().BeTrue();
        cookie.Contains("secure", StringComparison.OrdinalIgnoreCase).Should().BeTrue();
        cookie.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase).Should().BeTrue();
    }

    private static Mock<UserManager<AppUser>> MockUserManager()
    {
        var store = new Mock<IUserStore<AppUser>>();
        return new Mock<UserManager<AppUser>>(
            store.Object,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);
    }
}
