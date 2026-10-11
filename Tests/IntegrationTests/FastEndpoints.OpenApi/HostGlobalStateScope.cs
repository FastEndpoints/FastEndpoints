using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using FastEndpoints;
using FluentValidation;

namespace OpenApi;

sealed class HostGlobalStateScope : IDisposable
{
    readonly IServiceResolver? _resolver = MainExtensions.HostServiceResolver;
    readonly bool _serializerConfigured = MainExtensions.SerializerConfigured;
    readonly JsonSerializerOptions _serializerOptions = Config.SerOpts.Options;
    readonly Func<Type, MemberInfo, LambdaExpression, string> _nameResolver = ValidatorOptions.Global.PropertyNameResolver;

    public void Dispose()
    {
        Config.SerOpts.Options = _serializerOptions;
        MainExtensions.SerializerConfigured = _serializerConfigured;
        ValidatorOptions.Global.PropertyNameResolver = _nameResolver;
        MainExtensions.HostServiceResolver = _resolver;
    }
}
