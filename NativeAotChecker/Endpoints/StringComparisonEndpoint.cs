using FastEndpoints;
using System.Globalization;

namespace NativeAotChecker.Endpoints;

// Request for string comparison tests
public class StringComparisonRequest
{
    public string StringA { get; set; } = string.Empty;
    public string StringB { get; set; } = string.Empty;
    public string Culture { get; set; } = "en-US";
    public bool IgnoreCase { get; set; }
}

public class StringComparisonResponse
{
    public bool OrdinalEquals { get; set; }
    public bool CultureEquals { get; set; }
    public int OrdinalCompare { get; set; }
    public int CultureCompare { get; set; }
    public bool StartsWithResult { get; set; }
    public bool ContainsResult { get; set; }
    public bool StringComparisonWorked { get; set; }
}

/// <summary>
/// Tests culture-sensitive string operations in AOT mode.
/// AOT ISSUE: Culture-sensitive comparisons need ICU or NLS data.
/// CompareInfo.GetCompareInfo uses reflection for culture lookup.
/// StringComparison enum handling with culture needs resources.
/// </summary>
public class StringComparisonEndpoint : Endpoint<StringComparisonRequest, StringComparisonResponse>
{
    public override void Configure()
    {
        Post("string-comparison-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(StringComparisonRequest req, CancellationToken ct)
    {
        var comparison = req.IgnoreCase 
            ? StringComparison.OrdinalIgnoreCase 
            : StringComparison.Ordinal;

        bool cultureEquals = false;
        int cultureCompare = 0;

        try
        {
            var culture = CultureInfo.GetCultureInfo(req.Culture);
            var compareInfo = culture.CompareInfo;
            
            var options = req.IgnoreCase ? CompareOptions.IgnoreCase : CompareOptions.None;
            cultureEquals = compareInfo.Compare(req.StringA, req.StringB, options) == 0;
            cultureCompare = compareInfo.Compare(req.StringA, req.StringB, options);
        }
        catch
        {
            // Culture not found
        }

        await Send.OkAsync(new StringComparisonResponse
        {
            OrdinalEquals = string.Equals(req.StringA, req.StringB, comparison),
            CultureEquals = cultureEquals,
            OrdinalCompare = string.Compare(req.StringA, req.StringB, comparison),
            CultureCompare = cultureCompare,
            StartsWithResult = req.StringA.StartsWith(req.StringB, comparison),
            ContainsResult = req.StringA.Contains(req.StringB, comparison),
            StringComparisonWorked = true
        });
    }
}
