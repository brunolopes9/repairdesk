namespace RepairDesk.Common.Results;

public readonly record struct Result<T>(T? Value, string? ErrorCode, string? ErrorMessage)
{
    public bool IsSuccess => ErrorCode is null;
    public bool IsFailure => !IsSuccess;

    public static Result<T> Ok(T value) => new(value, null, null);
    public static Result<T> Fail(string code, string message) => new(default, code, message);
}

public readonly record struct Result(string? ErrorCode, string? ErrorMessage)
{
    public bool IsSuccess => ErrorCode is null;
    public bool IsFailure => !IsSuccess;

    public static Result Ok() => new(null, null);
    public static Result Fail(string code, string message) => new(code, message);
}
