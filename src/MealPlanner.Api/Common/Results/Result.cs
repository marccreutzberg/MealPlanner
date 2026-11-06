namespace MealPlanner.Api.Common.Results;

/// <summary>An expected failure: something the caller can act on, not an exception.</summary>
public sealed record Error(ErrorKind Kind, string Message)
{
    public static Error NotFound(string message) => new(ErrorKind.NotFound, message);
    public static Error Invalid(string message) => new(ErrorKind.Invalid, message);
}

public enum ErrorKind
{
    NotFound,
    Invalid,
}

/// <summary>Result pattern for handlers shared by HTTP endpoints and MCP tools (spec 7).</summary>
public sealed class Result<T>
{
    private Result(T? value, Error? error) => (Value, Error) = (value, error);

    public T? Value { get; }
    public Error? Error { get; }
    public bool IsSuccess => Error is null;

    public static implicit operator Result<T>(T value) => new(value, null);
    public static implicit operator Result<T>(Error error) => new(default, error);
}
