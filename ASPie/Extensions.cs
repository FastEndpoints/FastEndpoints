using System.Linq.Expressions;
using System.Reflection;

namespace ASPie
{
    public static class Extensions
    {
        private static Delegate CreateDelegate(this MethodInfo methodInfo, object target) //https://stackoverflow.com/a/40579063/4368485
        {
            Func<Type[], Type> getType;
            var isAction = methodInfo.ReturnType.Equals(typeof(void));
            var types = methodInfo.GetParameters().Select(p => p.ParameterType);

            if (isAction)
            {
                getType = Expression.GetActionType;
            }
            else
            {
                getType = Expression.GetFuncType;
                types = types.Concat(new[] { methodInfo.ReturnType });
            }

            if (methodInfo.IsStatic)
            {
                return Delegate.CreateDelegate(getType(types.ToArray()), methodInfo);
            }

            return Delegate.CreateDelegate(getType(types.ToArray()), target, methodInfo.Name);
        }

        public static DelegateEndpointConventionBuilder UseASPie(this IEndpointRouteBuilder e) //todo: add ref to Microsoft.AspNetCore.Routing and change SDK to Microsoft.NET.Sdk
        {
            var type = typeof(MyHandler);

            var methodInfo = type.GetMethod("HandleAsync", BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public);

            if (methodInfo == null)
                throw new ArgumentException($"Unable to find a `HandleAsync` method on: [{type.AssemblyQualifiedName}]");

            var instance = Activator.CreateInstance(type);

            if (instance == null)
                throw new InvalidOperationException("");

            var del = methodInfo.CreateDelegate(instance);

            return e.MapGet("/test/{id}", del);
        }
    }
}
