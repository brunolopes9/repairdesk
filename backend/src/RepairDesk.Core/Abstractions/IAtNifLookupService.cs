namespace RepairDesk.Core.Abstractions;

public interface IAtNifLookupService
{
    Task<AtNifLookupResult?> LookupAsync(string nif, CancellationToken ct = default);
}

public interface IAtNifRemoteClient
{
    Task<AtNifLookupResult?> LookupAsync(string nif, CancellationToken ct = default);
}

public sealed record AtNifLookupResult(
    string Nif,
    string Nome,
    string? Morada,
    string Status,
    DateTimeOffset CheckedAtUtc);

public sealed class AtNifRateLimitExceededException : Exception
{
    public AtNifRateLimitExceededException(int limit)
        : base($"AT NIF lookup daily limit exceeded ({limit}).")
    {
        Limit = limit;
    }

    public int Limit { get; }
}

public sealed class AtNifUnavailableException : Exception
{
    public AtNifUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
