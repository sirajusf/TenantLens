namespace LeaseLense.Web.Models.Properties;

public sealed class PropertyDirectoryPageViewModel
{
    public string? QueryText { get; init; }
    public IReadOnlyList<PropertyDirectoryItemViewModel> Properties { get; init; } = [];
}
