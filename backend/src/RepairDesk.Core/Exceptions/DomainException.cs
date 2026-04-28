namespace RepairDesk.Core.Exceptions;

public class DomainException : Exception
{
    public string Code { get; }

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
