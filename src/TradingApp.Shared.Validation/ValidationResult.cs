namespace TradingApp.Shared.Validation;

public sealed class ValidationResult
{
    private readonly List<string> errors = new();

    public IReadOnlyCollection<string> Errors => errors;
    public bool IsValid => errors.Count == 0;

    public void AddError(string error)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            errors.Add(error);
        }
    }

    public void AddErrors(IEnumerable<string> validationErrors)
    {
        foreach (var error in validationErrors)
        {
            AddError(error);
        }
    }
}