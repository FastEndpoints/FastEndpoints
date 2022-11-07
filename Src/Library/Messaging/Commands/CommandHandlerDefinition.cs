using System.Reflection;

namespace FastEndpoints;

public static partial class CommandExtensions
{
    internal class CommandHandlerDefinition
    {
        internal Type HandlerType { get; init; }
        internal MethodInfo? ExecuteMethod { get; set; }

        internal CommandHandlerDefinition(Type handlerType)
        {
            HandlerType = handlerType;
        }
    }
}