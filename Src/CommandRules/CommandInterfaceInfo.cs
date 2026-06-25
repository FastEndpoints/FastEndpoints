using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace FastEndpoints;

sealed record CommandInterfaceInfo(bool ImplementsStreamCommand, bool ImplementsGenericCommand, bool IsVoidCommand)
{
    static readonly ConcurrentDictionary<Type, CommandInterfaceInfo> _cache = new();

    public bool IsResultCommand => ImplementsGenericCommand && !IsVoidCommand;

    public static CommandInterfaceInfo For(Type commandType)
        => _cache.GetOrAdd(commandType, Create);

    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Command rules inspects runtime command interfaces to select dispatch behavior.")]
    static CommandInterfaceInfo Create(Type commandType)
    {
        var interfaces = commandType.GetInterfaces();
        var implementsStreamCommand = interfaces.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStreamCommand<>));
        var implementsGenericCommand = interfaces.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>));

        return new(implementsStreamCommand, implementsGenericCommand, typeof(ICommand).IsAssignableFrom(commandType));
    }
}