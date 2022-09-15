using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

///// <summary>
///// implement this interface on custom types you want to use with request dto model binding for route/query/form fields
///// </summary>
///// <typeparam name="TSelf"></typeparam>
//public interface IParseable<TSelf> where TSelf : notnull
//{
//    [RequiresPreviewFeatures]
//    static abstract bool TryParse(string? input, out TSelf? output);
//}

internal interface IServiceResolver
{
    static IServiceProvider RootServiceProvider { get; set; } //set only from .UseFastEndpoints() during startup

    IServiceScope CreateScope();

    TService? TryResolve<TService>() where TService : class;
    object? TryResolve(Type typeOfService);

    TService Resolve<TService>() where TService : class;
    object Resolve(Type typeOfService);
}
