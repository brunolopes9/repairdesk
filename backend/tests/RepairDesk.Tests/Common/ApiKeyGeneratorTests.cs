using FluentAssertions;
using RepairDesk.Common.Helpers;

namespace RepairDesk.Tests.Common;

public class ApiKeyGeneratorTests
{
    [Fact]
    public void Generate_ProducesExpectedShape()
    {
        var (plain, hash, prefix) = ApiKeyGenerator.Generate();
        plain.Should().StartWith("rd_live_");
        plain.Length.Should().Be("rd_live_".Length + ApiKeyGenerator.SuffixLength);
        hash.Should().HaveLength(64);  // SHA256 hex
        hash.Should().MatchRegex("^[0-9a-f]{64}$");
        prefix.Should().StartWith("rd_live_");
        prefix.Should().EndWith("…");
    }

    [Fact]
    public void Hash_IsDeterministic()
    {
        var input = "rd_live_AbCdEfGhIjKlMnOpQrStUvWxYz234567";
        ApiKeyGenerator.Hash(input).Should().Be(ApiKeyGenerator.Hash(input));
    }

    [Fact]
    public void Hash_DifferentInputs_ProduceDifferentHashes()
    {
        ApiKeyGenerator.Hash("rd_live_aaaaaaaa")
            .Should().NotBe(ApiKeyGenerator.Hash("rd_live_bbbbbbbb"));
    }

    [Fact]
    public void Generate_TwoInvocations_ProduceUniquePlainKeys()
    {
        var (plain1, _, _) = ApiKeyGenerator.Generate();
        var (plain2, _, _) = ApiKeyGenerator.Generate();
        plain1.Should().NotBe(plain2);
    }

    [Theory]
    [InlineData("rd_live_abc", true)]
    [InlineData("rd_test_abc", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("bearer xxxx", false)]
    public void LooksLikeApiKey_DetectsCorrectly(string? input, bool expected)
    {
        ApiKeyGenerator.LooksLikeApiKey(input).Should().Be(expected);
    }
}
