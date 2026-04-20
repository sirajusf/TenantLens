using System.ComponentModel.DataAnnotations;

namespace LeaseLense.Web.Models.ScamReports;

public sealed class CreateScamReportViewModel : IValidatableObject
{
    public Guid? PropertyId { get; init; }

    public bool PropertyNotListed { get; init; }

    [StringLength(510)]
    public string? NewPropertyTitle { get; init; }

    [StringLength(510)]
    public string? NewPropertyStreetAddress { get; init; }

    [StringLength(200)]
    public string? NewPropertyCity { get; init; }

    [StringLength(200)]
    public string? NewPropertyCountry { get; init; }

    [Required]
    [StringLength(200)]
    public string ScamType { get; init; } = string.Empty;

    [StringLength(4000)]
    public string? Description { get; init; }

    [Range(1, 5)]
    public decimal? SeverityScore { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!PropertyNotListed && !PropertyId.HasValue)
        {
            yield return new ValidationResult("Please select a listed property or choose 'Property not listed'.", [nameof(PropertyId)]);
        }

        if (PropertyNotListed)
        {
            if (string.IsNullOrWhiteSpace(NewPropertyTitle))
            {
                yield return new ValidationResult("Property title is required.", [nameof(NewPropertyTitle)]);
            }

            if (string.IsNullOrWhiteSpace(NewPropertyStreetAddress))
            {
                yield return new ValidationResult("Street address is required.", [nameof(NewPropertyStreetAddress)]);
            }

            if (string.IsNullOrWhiteSpace(NewPropertyCity))
            {
                yield return new ValidationResult("City is required.", [nameof(NewPropertyCity)]);
            }

            if (string.IsNullOrWhiteSpace(NewPropertyCountry))
            {
                yield return new ValidationResult("Country is required.", [nameof(NewPropertyCountry)]);
            }
        }
    }
}
