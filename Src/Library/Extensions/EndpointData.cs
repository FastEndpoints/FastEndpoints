using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;

namespace FastEndpoints;

internal sealed class EndpointData
{
    //using Lazy<T> to prevent contention when WAF testing (see issue #10)
    private readonly Lazy<EndpointDefinition[]> _endpoints;

    internal EndpointDefinition[] Found => _endpoints.Value;

    internal static Stopwatch Stopwatch { get; } = new();

    internal EndpointData(IServiceCollection services, IEnumerable<Assembly>? assemblies)
    {
        _endpoints = new(() =>
        {
            var endpoints = BuildEndpointDefinitions(services, assemblies ?? Enumerable.Empty<Assembly>());

            if (endpoints.Length == 0)
                throw new InvalidOperationException("FastEndpoints was unable to find any endpoint declarations!");

            return endpoints!;
        });

        //need this here to cause the lazy factory to run now.
        //cause the endpoints are being added to DI container within the factory
        _ = _endpoints.Value;
    }

    private static EndpointDefinition[] BuildEndpointDefinitions(IServiceCollection services, IEnumerable<Assembly> assemblies)
    {
        Stopwatch.Start();

        var excludes = new[]
        {
                "Microsoft.",
                "System.",
                "FastEndpoints.",
                "testhost",
                "netstandard",
                "Newtonsoft.",
                "mscorlib",
                "NuGet.",
                "NSwag."
        };

        var discoveredTypes = AppDomain.CurrentDomain
            .GetAssemblies()
            .Union(assemblies)
            .Where(a =>
                !a.IsDynamic &&
                !excludes.Any(n => a.FullName!.StartsWith(n)))
            .SelectMany(a => a.GetTypes())
            .Where(t =>
                !t.IsAbstract &&
                !t.IsInterface &&
                t.GetInterfaces().Intersect(new[] {
                        Types.IEndpoint,
                        Types.IValidator,
                        Types.IEventHandler
                }).Any());

        //Endpoint<TRequest>
        //Validator<TRequest>

        var epList = new List<(Type tEndpoint, Type tRequest)>();

        //key: TRequest //val: TValidator
        var valDict = new Dictionary<Type, Type>();

        foreach (var tDisc in discoveredTypes)
        {
            foreach (var tInterface in tDisc.GetInterfaces())
            {
                if (tInterface == Types.IEndpoint)
                {
                    var tRequest = Types.EmptyRequest;

                    if (tDisc.BaseType?.IsGenericType is true)
                        tRequest = tDisc.BaseType?.GetGenericArguments()?[0] ?? tRequest;

                    services.AddTransient(tDisc);
                    epList.Add((tDisc, tRequest));
                    continue;
                }

                if (tInterface == Types.IValidator)
                {
                    Type tRequest = tDisc.BaseType?.GetGenericArguments()[0]!;
                    valDict.Add(tRequest, tDisc);
                    continue;
                }

                if (tInterface == Types.IEventHandler)
                {
                    ((IEventHandler?)Activator.CreateInstance(tDisc))?.Subscribe();
                    continue;
                }
            }
        }

        return epList.Select(x =>
        {
            var def = new EndpointDefinition()
            {
                EndpointType = x.tEndpoint,
                ValidatorType = valDict.GetValueOrDefault(x.tRequest),
                ReqDtoType = x.tRequest,
            };

            var serviceBoundEpProps = def.EndpointType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => p.CanRead && p.CanWrite)
                .Select(p => new ServiceBoundEpProp()
                {
                    PropSetter = def.EndpointType.SetterForProp(p.Name),
                    PropType = p.PropertyType,
                })
                .ToArray();

            if (serviceBoundEpProps.Length > 0)
                def.ServiceBoundEpProps = serviceBoundEpProps;

            var implementsConfigureMethod = false;
            var implementsHandleAsync = false;
            var implementsExecuteAsync = false;

            foreach (var m in x.tEndpoint.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy))
            {
                if (m.Name == "Configure" && !m.IsDefined(Types.NotImplementedAttribute, false))
                    implementsConfigureMethod = true;

                if (m.Name == "HandleAsync" && !m.IsDefined(Types.NotImplementedAttribute, false))
                    implementsHandleAsync = true;

                if (m.Name == "ExecuteAsync" && !m.IsDefined(Types.NotImplementedAttribute, false))
                    implementsExecuteAsync = true;
            }

            var epAttribs = x.tEndpoint
                .GetCustomAttributes(true)
                .Where(a => a is HttpAttribute or AllowAnonymousAttribute or AuthorizeAttribute)
                .ToArray();

            if (implementsConfigureMethod && epAttribs.Length > 0)
                throw new InvalidOperationException($"The endpoint [{x.tEndpoint.FullName}] has both Configure() method and attribute decorations on the class level. Only one of those strategies should be used!");

            if (epAttribs.Length > 0 && !epAttribs.Any(a => a is HttpAttribute))
                throw new InvalidOperationException($"The endpoint [{x.tEndpoint.FullName}] should have at least one http method attribute such as [HttpGet(...)]");

            if (!implementsHandleAsync && !implementsExecuteAsync)
                throw new InvalidOperationException($"The endpoint [{x.tEndpoint.FullName}] must implement either [HandleAsync] or [ExecuteAsync] methods!");

            if (implementsHandleAsync && implementsExecuteAsync)
                throw new InvalidOperationException($"The endpoint [{x.tEndpoint.FullName}] has both [HandleAsync] and [ExecuteAsync] methods implemented!");

            def.ExecuteAsyncImplemented = implementsExecuteAsync;

            //create an endpoint instance and run the Configure() method in order to get the def object populated
            BaseEndpoint? instance =
                x.tEndpoint.GetConstructor(Type.EmptyTypes) is null
                ? (BaseEndpoint)FormatterServices.GetUninitializedObject(x.tEndpoint)! //this is an endpoint with ctor arguments
                : (BaseEndpoint)Activator.CreateInstance(x.tEndpoint)!; //endpoint which has a default ctor

            instance.Configuration = def;

            if (implementsConfigureMethod)
            {
                instance.Configure();
            }
            else
            {
                foreach (var att in epAttribs)
                {
                    if (att is HttpAttribute http)
                    {
                        instance.Verbs(http.Verb);
                        instance.Configuration.Routes = new[] { http.Route };
                    }
                    if (att is AllowAnonymousAttribute)
                    {
                        instance.Configuration.AnonymousVerbs =
                            instance.Configuration.Verbs?.Length > 0
                            ? instance.Configuration.Verbs.Select(v => v).ToArray()
                            : Enum.GetNames(Types.Http);
                    }
                    if (att is AuthorizeAttribute auth)
                    {
                        instance.Configuration.Roles = auth.Roles?.Split(',');
                        instance.Configuration.AuthSchemes = auth.AuthenticationSchemes?.Split(',');
                        if (auth.Policy is not null) instance.Configuration.PreBuiltUserPolicies = new[] { auth.Policy };
                    }
                }
            }

            if (instance.Configuration.ValidatorType is not null)
            {
                if (instance.Configuration.ScopedValidator)
                    services.AddScoped(instance.Configuration.ValidatorType);
                else
                    services.AddSingleton(instance.Configuration.ValidatorType);
            }

            return def;
        }).ToArray();
    }
}