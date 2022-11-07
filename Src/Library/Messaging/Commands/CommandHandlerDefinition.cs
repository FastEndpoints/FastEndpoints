using System.Reflection;

namespace FastEndpoints;

public static partial class CommandExtensions
{
    internal class CommandHandlerDefinition
    {
        internal Type HandlerType { get; init; }
        internal MethodInfo ExecuteMethod { get; init; }

        internal CommandHandlerDefinition(Type handlerType)
        {
            HandlerType = handlerType;
            ExecuteMethod = handlerType.GetMethod(
                "ExecuteAsync",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy)!;
        }
    }
}