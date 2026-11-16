namespace RepairDesk.Core.Exceptions;

public class DomainException : Exception
{
    public string Code { get; }

    /// <summary>Sprint 683: dados para a UI mostrar o erro no sítio certo (ex.: a linha da venda sem stock).</summary>
    public IReadOnlyDictionary<string, object?>? Detalhes { get; init; }

    public DomainException(string code, string message) : base(message)
    {
        Code = code;
    }
}

public sealed class NotFoundException(string entity, object key)
    : DomainException("not_found", $"{entity} with key '{key}' not found.");

public sealed class ConflictException(string code, string message)
    : DomainException(code, message);

public sealed class ValidationException(string code, string message)
    : DomainException(code, message);

public sealed class ForbiddenException(string code, string message)
    : DomainException(code, message);
