using EZEndpoints.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Reflection;
using System.Text;

namespace EZEndpoints
{
    public static class Extensions
    {
        private static (Type type, object? instance, string? name)[]? endpoints;

        public static IServiceCollection AddAuthenticationJWTBearer(this IServiceCollection services, string tokenSigningKey)
        {
            services.AddAuthentication(o =>
            {
                o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(o =>
            {
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    ValidateAudience = false,
                    ValidateIssuer = false,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(tokenSigningKey))
                };
            });

            return services;
        }

        public static IServiceCollection AddEZEndpoints(this IServiceCollection services) //add ref to Microsoft.AspNetCore.Builder and change SDK to Microsoft.NET.Sdk
        {
            endpoints = DiscoverEndpointTypes()
                .Select(t => (t, Activator.CreateInstance(t), t.AssemblyQualifiedName)).ToArray();

            services.AddAuthorization(BuildPermissionPolicies);

            return services;
        }

        public static IEndpointRouteBuilder UseEZEndpoints(this IEndpointRouteBuilder builder)
        {
            Endpoint.serviceProvider = builder.ServiceProvider;

            if (endpoints is null) throw new InvalidOperationException("Please use .UseEZEndpoints() first!");

            foreach (var (type, instance, name) in endpoints)
            {
                var method = type.GetMethod(nameof(Endpoint.ExecAsync), BindingFlags.Instance | BindingFlags.NonPublic);

                if (method is null) throw new InvalidOperationException($"Unable to find the `ExecAsync` method on: [{name}]");

                if (instance is null) throw new InvalidOperationException($"Unable to create an instance of: [{name}]");

                var verbs = type.GetFieldValues(nameof(Endpoint.verbs), instance);
                if (verbs?.Any() != true) throw new ArgumentException($"No HTTP Verbs declared on: [{name}]");

                var routes = type.GetFieldValues(nameof(Endpoint.routes), instance);
                if (routes?.Any() != true) throw new ArgumentException($"No Routes declared on: [{name}]");

                var deligate = Delegate.CreateDelegate(typeof(RequestDelegate), instance, method);

                string? permissionPolicyName = null;
                var permissions = type.GetFieldValues(nameof(Endpoint.permissions), instance);
                if (permissions?.Any() is true) permissionPolicyName = $"{Claim.Permissions}:{name}";

                var userPolicies = type.GetFieldValues(nameof(Endpoint.policies), instance);
                var policiesToAdd = new List<string>();
                if (userPolicies?.Any() is true) policiesToAdd.AddRange(userPolicies);
                if (permissionPolicyName != null) policiesToAdd.Add(permissionPolicyName);

                var userRoles = type.GetFieldValues(nameof(Endpoint.roles), instance);
                var rolesToAdd = userRoles?.Any() is true ? string.Join(',', userRoles) : null;

                foreach (var route in routes)
                {
                    var eb = builder.MapMethods(route, verbs, deligate)
                                    .RequireAuthorization(); //secure by default

                    var allowAnnonymous = (bool?)type.GetFieldValue(nameof(Endpoint.allowAnnonymous), instance);
                    if (allowAnnonymous is true) eb.AllowAnonymous();

                    if (policiesToAdd.Count > 0) eb.RequireAuthorization(policiesToAdd.ToArray());

                    if (rolesToAdd != null) eb.RequireAuthorization(new AuthorizeData { Roles = rolesToAdd });
                }
            }
            endpoints = null;
            return builder;
        }

        private static void BuildPermissionPolicies(AuthorizationOptions options)
        {
            if (endpoints is null) return;

            foreach (var (type, instance, name) in endpoints)
            {
                var permissions = type.GetFieldValues(nameof(Endpoint.permissions), instance);

                if (permissions?.Any() is true)
                {
                    var policyName = $"{Claim.Permissions}:{name}";
                    var allowAnyPermission = (bool?)type.GetFieldValue(nameof(Endpoint.allowAnyPermission), instance);
                    if (allowAnyPermission is true)
                    {
                        options.AddPolicy(policyName, b =>
                        {
                            b.RequireAssertion(x =>
                            {
                                var hasAny = x.User.Claims
                                .FirstOrDefault(c => c.Type == Claim.Permissions)?
                                .Value
                                .Split(',')
                                .Intersect(permissions)
                                .Any();
                                return hasAny is true;
                            });
                        });
                    }
                    else
                    {
                        options.AddPolicy(policyName, b =>
                        {
                            b.RequireAssertion(x =>
                            {
                                var hasAll = !x.User.Claims
                                .FirstOrDefault(c => c.Type == Claim.Permissions)?
                                .Value
                                .Split(',')
                                .Except(permissions)
                                .Any();
                                return hasAll is true;
                            });
                        });
                    }
                }
            }
        }

        private static IEnumerable<Type> DiscoverEndpointTypes()
        {
            var excludes = new[]
                {
                    "Microsoft.",
                    "System.",
                    "MongoDB.",
                    "testhost",
                    "netstandard",
                    "Newtonsoft.",
                    "mscorlib",
                    "NuGet."
                };

            var types = AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(a =>
                      !a.IsDynamic &&
                      !excludes.Any(n => a.FullName.StartsWith(n)))
                .SelectMany(a => a.GetTypes())
                .Where(t =>
                      !t.IsAbstract &&
                       t.GetInterfaces().Contains(typeof(IEndpoint)));

            if (!types.Any())
                throw new InvalidOperationException("Unable to find any endpoint declarations!");

            return types;
        }

        private static IEnumerable<string>? GetFieldValues(this Type type, string fieldName, object? instance)
        {
            return type.BaseType?
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)?
                .GetValue(instance) as IEnumerable<string>;
        }

        private static object? GetFieldValue(this Type type, string fieldName, object? instance)
        {
            return type.BaseType?
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)?
                .GetValue(instance);
        }
    }
}

//todo: write tests
//todo: add xml documentation
//todo: [From(Claim.ClaimType)] attribute - should forbid if current user doesn't have claim
//todo: ctx.Send... files and bytes
