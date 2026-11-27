using System.ComponentModel.DataAnnotations;

namespace DevPortfolio.DTOs;

/// <summary>
/// Accepts null/empty, or an absolute http(s) URL. Anything else (notably
/// <c>javascript:</c> and <c>data:</c> URLs) is rejected, because these values are
/// rendered as link hrefs on public profile pages viewed by other people.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class HttpUrlAttribute : ValidationAttribute
{
    public static bool IsValidUrl(string? value) =>
        string.IsNullOrWhiteSpace(value)
        || (Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));

    protected override ValidationResult? IsValid(object? value, ValidationContext context) =>
        value is null || (value is string s && IsValidUrl(s))
            ? ValidationResult.Success
            : new ValidationResult($"{context.DisplayName} must be a full http:// or https:// URL.");
}

public static class InputExtensions
{
    /// <summary>Trims, and turns blank strings into null so optional fields stay NULL in the DB.</summary>
    public static string? NullIfBlank(this string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
