namespace ExtShiftingApp.Shift;

public record ParseResult
{
    public bool IsValid { get; }
    public IReadOnlyList<IReadOnlyList<int>>? Simplices { get; }
    public string? Error { get; }

    private ParseResult(bool isValid, IReadOnlyList<IReadOnlyList<int>>? simplices, string? error)
    {
        IsValid = isValid;
        Simplices = simplices;
        Error = error;
    }

    public static ParseResult Success(IReadOnlyList<IReadOnlyList<int>> simplices) =>
        new(true, simplices, null);

    public static ParseResult Failure(string error) =>
        new(false, null, error);
}
