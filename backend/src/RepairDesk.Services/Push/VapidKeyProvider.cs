using Microsoft.Extensions.Options;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using WebPush;

namespace RepairDesk.Services.Push;

public interface IVapidKeyProvider
{
    Task<VapidKeys> GetKeysAsync(CancellationToken ct = default);
}

public class VapidKeyProvider : IVapidKeyProvider
{
    private const string PublicKeySetting = "Push:VapidPublicKey";
    private const string PrivateKeySetting = "Push:VapidPrivateKey";
    private const string SubjectSetting = "Push:VapidSubject";

    private readonly ISystemSettingRepository _settings;
    private readonly IOptions<PushOptions> _options;

    public VapidKeyProvider(ISystemSettingRepository settings, IOptions<PushOptions> options)
    {
        _settings = settings;
        _options = options;
    }

    public async Task<VapidKeys> GetKeysAsync(CancellationToken ct = default)
    {
        var configuredPublic = _options.Value.VapidPublicKey;
        var configuredPrivate = _options.Value.VapidPrivateKey;
        var configuredSubject = NormalizeSubject(_options.Value.Subject);

        if (!string.IsNullOrWhiteSpace(configuredPublic) && !string.IsNullOrWhiteSpace(configuredPrivate))
            return new VapidKeys(configuredPublic.Trim(), configuredPrivate.Trim(), configuredSubject);

        var publicSetting = await _settings.FindAsync(PublicKeySetting, ct);
        var privateSetting = await _settings.FindAsync(PrivateKeySetting, ct);
        var subjectSetting = await _settings.FindAsync(SubjectSetting, ct);

        if (publicSetting is not null && privateSetting is not null)
        {
            return new VapidKeys(
                publicSetting.Value,
                privateSetting.Value,
                NormalizeSubject(subjectSetting?.Value ?? configuredSubject));
        }

        var generated = VapidHelper.GenerateVapidKeys();
        await UpsertAsync(publicSetting, PublicKeySetting, generated.PublicKey, ct);
        await UpsertAsync(privateSetting, PrivateKeySetting, generated.PrivateKey, ct);
        await UpsertAsync(subjectSetting, SubjectSetting, configuredSubject, ct);
        await _settings.SaveAsync(ct);

        return new VapidKeys(generated.PublicKey, generated.PrivateKey, configuredSubject);
    }

    private async Task UpsertAsync(SystemSetting? setting, string key, string value, CancellationToken ct)
    {
        if (setting is null)
        {
            await _settings.AddAsync(new SystemSetting { Key = key, Value = value }, ct);
            return;
        }

        setting.Value = value;
        setting.UpdatedAt = DateTime.UtcNow;
    }

    private static string NormalizeSubject(string? subject)
        => string.IsNullOrWhiteSpace(subject) ? "mailto:suporte@repairdesk.pt" : subject.Trim();
}
