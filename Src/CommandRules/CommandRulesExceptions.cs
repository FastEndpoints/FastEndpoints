namespace FastEndpoints;

/// <summary>
/// base exception type for command rules failures.
/// </summary>
public class CommandRuleException : Exception
{
    /// <summary>
    /// initializes a new command rule exception.
    /// </summary>
    /// <param name="message">exception message.</param>
    public CommandRuleException(string message) : base(message) { }

    /// <summary>
    /// initializes a new command rule exception with an inner exception.
    /// </summary>
    /// <param name="message">exception message.</param>
    /// <param name="innerException">the exception that caused this exception.</param>
    public CommandRuleException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// exception thrown when no command rule matches an input and unhandled inputs are configured to throw.
/// </summary>
public sealed class CommandRuleNotFoundException : CommandRuleException
{
    /// <summary>
    /// initializes a new command rule not found exception.
    /// </summary>
    /// <param name="inputType">the input type that did not match any rule.</param>
    public CommandRuleNotFoundException(Type inputType) : base($"No command rule matched input type [{inputType.FullName}].")
    {
        InputType = inputType;
    }

    /// <summary>
    /// the input type that did not match any rule.
    /// </summary>
    public Type InputType { get; }
}

/// <summary>
/// exception thrown when a planned command cannot be dispatched by the selected dispatch mode.
/// </summary>
public sealed class UnsupportedPlannedCommandException : CommandRuleException
{
    /// <summary>
    /// initializes a new unsupported planned command exception.
    /// </summary>
    /// <param name="commandType">the unsupported command type.</param>
    /// <param name="reason">the reason the command is unsupported.</param>
    public UnsupportedPlannedCommandException(Type commandType, string reason) : base($"Command [{commandType.FullName}] cannot be dispatched by command rules. {reason}")
    {
        CommandType = commandType;
    }

    /// <summary>
    /// the unsupported command type.
    /// </summary>
    public Type CommandType { get; }
}