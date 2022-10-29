namespace FastEndpoints;

/// <summary>
/// dto used to hold the result of a value parsing operation
/// </summary>
public struct ParseResult
{
    /// <summary>
    /// will be true if the parsing operation was a success
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// will hold the parsed value if the parsing was successful
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// constructor for initializsing a ParseResult instance
    /// </summary>
    /// <param name="isSuccess">set to true of parsing was successful</param>
    /// <param name="value">set the value that was obtained from the parsing operation</param>
    public ParseResult(bool isSuccess, object? value)
    {
        IsSuccess = isSuccess;
        Value = value;
    }
}