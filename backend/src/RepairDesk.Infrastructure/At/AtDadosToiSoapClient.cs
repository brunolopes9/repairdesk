using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepairDesk.Core.Abstractions;

namespace RepairDesk.Infrastructure.At;

public sealed class AtDadosToiSoapClient : IAtNifRemoteClient
{
    private readonly AtNifLookupOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<AtDadosToiSoapClient> _logger;

    public AtDadosToiSoapClient(
        IOptions<AtNifLookupOptions> options,
        TimeProvider clock,
        ILogger<AtDadosToiSoapClient> logger)
    {
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    public async Task<AtNifLookupResult?> LookupAsync(string nif, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.CertPath))
            throw new AtNifUnavailableException("AT certificate path is not configured.");

        var binding = new BasicHttpsBinding(BasicHttpsSecurityMode.Transport)
        {
            MaxReceivedMessageSize = 64 * 1024,
            SendTimeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 2, 30)),
            ReceiveTimeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 2, 30)),
            OpenTimeout = TimeSpan.FromSeconds(5),
            CloseTimeout = TimeSpan.FromSeconds(5),
        };
        binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Certificate;

        var endpoint = new EndpointAddress(_options.Endpoint);
        var factory = new ChannelFactory<IAtDadosToiPort>(binding, endpoint);
        factory.Credentials.ClientCertificate.Certificate = LoadCertificate();

        var channel = factory.CreateChannel();
        var clientChannel = (IClientChannel)channel;

        try
        {
            var response = await channel.ConsultarAsync(new AtDadosToiRequest { Nif = nif });
            clientChannel.Close();
            factory.Close();
            return Map(response, nif);
        }
        catch (FaultException ex) when (IsNotFoundFault(ex))
        {
            clientChannel.Abort();
            factory.Abort();
            _logger.LogInformation("AT returned no taxpayer data for {MaskedNif}", AtNifLookupService.MaskNif(nif));
            return null;
        }
        catch (Exception ex)
        {
            clientChannel.Abort();
            factory.Abort();
            throw new AtNifUnavailableException("AT NIF lookup webservice is unavailable.", ex);
        }
    }

    private AtNifLookupResult? Map(AtDadosToiResponse? response, string nif)
    {
        if (response is null || string.IsNullOrWhiteSpace(response.Nome))
            return null;

        return new AtNifLookupResult(
            nif,
            response.Nome.Trim(),
            string.IsNullOrWhiteSpace(response.Morada) ? null : response.Morada.Trim(),
            string.IsNullOrWhiteSpace(response.Status) ? "confirmed" : response.Status.Trim(),
            _clock.GetUtcNow());
    }

    private X509Certificate2 LoadCertificate()
    {
        var certPath = _options.CertPath!;
        if (!File.Exists(certPath))
            throw new AtNifUnavailableException("AT certificate file was not found.");

        if (certPath.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase) ||
            certPath.EndsWith(".p12", StringComparison.OrdinalIgnoreCase))
        {
            return new X509Certificate2(
                certPath,
                _options.KeyPassword,
                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet);
        }

        var cert = new X509Certificate2(certPath);
        if (string.IsNullOrWhiteSpace(_options.KeyPath))
            return cert;

        if (!File.Exists(_options.KeyPath))
            throw new AtNifUnavailableException("AT private key file was not found.");

        var keyPem = File.ReadAllText(_options.KeyPath);
        using var rsa = RSA.Create();
        if (string.IsNullOrEmpty(_options.KeyPassword))
            rsa.ImportFromPem(keyPem);
        else
            rsa.ImportFromEncryptedPem(keyPem, _options.KeyPassword);

        return cert.CopyWithPrivateKey(rsa);
    }

    private static bool IsNotFoundFault(FaultException ex) =>
        ex.Code.Name.Contains("NotFound", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("inexistente", StringComparison.OrdinalIgnoreCase);
}

[ServiceContract]
public interface IAtDadosToiPort
{
    [OperationContract(Action = "consultar", ReplyAction = "*")]
    Task<AtDadosToiResponse?> ConsultarAsync(AtDadosToiRequest request);
}

[DataContract(Namespace = "http://servicos.portaldasfinancas.gov.pt/sgdtoi/dadosTOI")]
public sealed class AtDadosToiRequest
{
    [DataMember(Order = 1, Name = "nif")]
    public string Nif { get; set; } = string.Empty;
}

[DataContract(Namespace = "http://servicos.portaldasfinancas.gov.pt/sgdtoi/dadosTOI")]
public sealed class AtDadosToiResponse
{
    [DataMember(Order = 1, Name = "nif")]
    public string? Nif { get; set; }

    [DataMember(Order = 2, Name = "nome")]
    public string? Nome { get; set; }

    [DataMember(Order = 3, Name = "morada")]
    public string? Morada { get; set; }

    [DataMember(Order = 4, Name = "status")]
    public string? Status { get; set; }
}
