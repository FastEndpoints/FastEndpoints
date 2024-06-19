using System.Text.Json;

namespace FastEndpoints;

public sealed class JsonBindException(string fieldName,
                                      string failureMessage,
                                      JsonException x)
    : JsonException(x.Message, x.Path, x.LineNumber, x.BytePositionInLine, x.InnerException)
{
    public string FieldName { get; } = fieldName;
    public string FailureMessage { get; } = failureMessage;
}