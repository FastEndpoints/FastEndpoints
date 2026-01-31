using FastEndpoints;
using System.Runtime.CompilerServices;

namespace NativeAotChecker.Endpoints;

// Request
public class FormattableStringRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string Culture { get; set; } = "en-US";
}

public class FormattableStringResponse
{
    public string FormattedDefault { get; set; } = string.Empty;
    public string FormattedInvariant { get; set; } = string.Empty;
    public string FormattedCulture { get; set; } = string.Empty;
    public string InterpolatedResult { get; set; } = string.Empty;
    public bool FormattableStringWorked { get; set; }
}

/// <summary>
/// Tests FormattableString and culture-specific formatting in AOT mode.
/// AOT ISSUE: FormattableString.Invariant uses runtime culture data.
/// Culture-specific formatting needs locale resources.
/// String interpolation with formatters uses IFormattable reflection.
/// </summary>
public class FormattableStringEndpoint : Endpoint<FormattableStringRequest, FormattableStringResponse>
{
    public override void Configure()
    {
        Post("formattable-string-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(FormattableStringRequest req, CancellationToken ct)
    {
        // Get FormattableString via helper
        FormattableString formattable = $"Hello {req.Name}, amount: {req.Amount:C}, date: {req.Date:D}";

        string formattedCulture;
        try
        {
            var culture = System.Globalization.CultureInfo.GetCultureInfo(req.Culture);
            formattedCulture = formattable.ToString(culture);
        }
        catch
        {
            formattedCulture = "Culture not found";
        }

        await Send.OkAsync(new FormattableStringResponse
        {
            FormattedDefault = formattable.ToString(),
            FormattedInvariant = FormattableString.Invariant(formattable),
            FormattedCulture = formattedCulture,
            InterpolatedResult = $"Name={req.Name}, Amount={req.Amount:F2}",
            FormattableStringWorked = true
        });
    }
}
