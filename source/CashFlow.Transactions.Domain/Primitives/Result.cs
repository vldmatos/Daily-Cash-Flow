namespace CashFlow.Transactions.Domain.Primitives;

public sealed class Result<T>
{
    private Result(T value)
    {
        Value = value;
        IsSuccess = true;
        Error = string.Empty;
    }

    private Result(string error)
    {
        Value = default!;
        IsSuccess = false;
        Error = error;
    }

    public T Value { get; }
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string Error { get; }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(string error) => new(error);
}

public sealed class Result
{
    private Result()
    {
        IsSuccess = true;
        Error = string.Empty;
    }

    private Result(string error)
    {
        IsSuccess = false;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string Error { get; }

    public static Result Success() => new();
    public static Result Failure(string error) => new(error);
}
