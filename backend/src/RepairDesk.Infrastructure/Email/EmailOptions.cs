using Microsoft.Extensions.Configuration;

namespace RepairDesk.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Remetente, ex.: "Mender &lt;no-reply@lopestech.pt&gt;". O domínio tem de estar verificado no Resend.</summary>
    public string From { get; init; } = string.Empty;

    public string ResendApiKey { get; init; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(From) && !string.IsNullOrWhiteSpace(ResendApiKey);

    public static EmailOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        return new EmailOptions
        {
            From = (section["From"] ?? string.Empty).Trim(),
            ResendApiKey = (section["ResendApiKey"] ?? string.Empty).Trim(),
        };
    }
}
