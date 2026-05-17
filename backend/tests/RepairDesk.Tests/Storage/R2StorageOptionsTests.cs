using FluentAssertions;
using Microsoft.Extensions.Configuration;
using RepairDesk.Infrastructure.Storage;

namespace RepairDesk.Tests.Storage;

public class R2StorageOptionsTests
{
    [Fact]
    public void FromConfiguration_ReadsValues_AndBuildsEndpoint()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Storage:R2:AccountId"] = " account-id ",
            ["Storage:R2:AccessKey"] = " access-key ",
            ["Storage:R2:Secret"] = " secret ",
            ["Storage:R2:Bucket"] = " repairdesk-dev-media ",
        });

        var options = R2StorageOptions.FromConfiguration(config);

        options.AccountId.Should().Be("account-id");
        options.AccessKey.Should().Be("access-key");
        options.Secret.Should().Be("secret");
        options.Bucket.Should().Be("repairdesk-dev-media");
        options.Endpoint.Should().Be("https://account-id.r2.cloudflarestorage.com");
        options.ValidateValues().Should().BeEmpty();
    }

    [Fact]
    public void Validate_ReturnsRequiredFieldErrors_WhenConfigMissing()
    {
        var options = R2StorageOptions.FromConfiguration(BuildConfig(new Dictionary<string, string?>()));

        var errors = options.ValidateValues();

        errors.Should().Contain(e => e.Contains("Storage:R2:AccountId", StringComparison.Ordinal));
        errors.Should().Contain(e => e.Contains("Storage:R2:AccessKey", StringComparison.Ordinal));
        errors.Should().Contain(e => e.Contains("Storage:R2:Secret", StringComparison.Ordinal));
        errors.Should().Contain(e => e.Contains("Storage:R2:Bucket", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("https://account.r2.cloudflarestorage.com")]
    [InlineData("account/path")]
    [InlineData("account id")]
    public void Validate_ReturnsError_WhenAccountIdLooksLikeEndpointOrPath(string accountId)
    {
        var config = ValidConfig();
        config["Storage:R2:AccountId"] = accountId;
        var options = R2StorageOptions.FromConfiguration(config);

        options.ValidateValues().Should().Contain(e => e.Contains("AccountId", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("bucket/path")]
    [InlineData("bucket name")]
    public void Validate_ReturnsError_WhenBucketLooksLikePath(string bucket)
    {
        var config = ValidConfig();
        config["Storage:R2:Bucket"] = bucket;
        var options = R2StorageOptions.FromConfiguration(config);

        options.ValidateValues().Should().Contain(e => e.Contains("Bucket", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ThrowsHelpfulException_WhenInvalid()
    {
        var options = R2StorageOptions.FromConfiguration(BuildConfig(new Dictionary<string, string?>()));

        var act = options.Validate;

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Storage:R2:AccountId*");
    }

    private static IConfigurationRoot ValidConfig() => BuildConfig(new Dictionary<string, string?>
    {
        ["Storage:R2:AccountId"] = "account-id",
        ["Storage:R2:AccessKey"] = "access-key",
        ["Storage:R2:Secret"] = "secret",
        ["Storage:R2:Bucket"] = "repairdesk-dev-media",
    });

    private static IConfigurationRoot BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
