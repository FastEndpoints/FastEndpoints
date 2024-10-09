using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

//TODO: optimize this abomination!!!

static class ComplexFormBinder
{
    internal static void Bind(PropCache fromFormProp, object requestDto, IFormCollection forms)
    {
        var propInstance = CachedObjectFactory(fromFormProp.PropType)();

        BindPropertiesRecursively(propInstance, forms, string.Empty);

        fromFormProp.PropSetter(requestDto, propInstance);
    }

    static void BindPropertiesRecursively(object obj, IFormCollection form, string prefix)
    {
        var properties = obj.GetType().BindableProps();

        foreach (var prop in properties)
        {
            var propName = prop.GetCustomAttribute<BindFromAttribute>()?.Name ?? prop.Name;
            var key = string.IsNullOrEmpty(prefix)
                          ? propName
                          : $"{prefix}.{propName}";

            if (Types.IFormFile.IsAssignableFrom(prop.PropertyType))
            {
                if (form.Files.GetFile(key) is { } file)
                    prop.SetValue(obj, file);
            }
            else if (Types.IEnumerableOfIFormFile.IsAssignableFrom(prop.PropertyType))
            {
                var files = form.Files.GetFiles(key);

                if (files.Count == 0)
                    continue;

                var collection = new FormFileCollection();
                collection.AddRange(files);
                prop.SetValue(obj, collection);
            }
            else if (prop.PropertyType.IsClass && prop.PropertyType != Types.String && !Types.IEnumerable.IsAssignableFrom(prop.PropertyType))
            {
                var nestedObject = CachedObjectFactory(prop.PropertyType)();

                BindPropertiesRecursively(nestedObject, form, key);
                prop.SetValue(obj, nestedObject);
            }
            else if (Types.IEnumerable.IsAssignableFrom(prop.PropertyType) && prop.PropertyType != Types.String)
            {
                var tElement = prop.PropertyType.IsGenericType
                                   ? prop.PropertyType.GetGenericArguments()[0]
                                   : prop.PropertyType.GetElementType();

                if (tElement is null)
                    continue;

                var list = (IList)CachedObjectFactory(Types.ListOf1.MakeGenericType(tElement))();

                var index = 0;

                while (true)
                {
                    var indexedKey = $"{key}[{index}]";
                    var item = CachedObjectFactory(tElement)();

                    BindPropertiesRecursively(item, form, indexedKey);

                    if (HasAnyPropertySet(item))
                    {
                        list.Add(item);
                        index++;
                    }
                    else
                        break;

                    static bool HasAnyPropertySet(object obj)
                    {
                        return obj.GetType().BindableProps().Any(
                            p =>
                            {
                                var val = p.GetValue(obj);

                                if (val is IEnumerable enm)
                                    return enm.Cast<object>().Any();

                                return val is not null && HasAnyPropertySet(val);
                            });
                    }
                }

                prop.SetValue(obj, list);
            }
            else
            {
                if (!form.TryGetValue(key, out var val))
                    continue;

                var res = prop.PropertyType.CachedValueParser()(val);
                if (res.IsSuccess)
                    prop.SetValue(obj, res.Value);
            }
        }
    }

    static readonly ConcurrentDictionary<Type, Func<object>> _objectFactoryCache = new();

    static Func<object> CachedObjectFactory(Type type)
    {
        return _objectFactoryCache.GetOrAdd(type, CompileObjectFactory);

        static Func<object> CompileObjectFactory(Type type)
        {
            var ctor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                           .MinBy(c => c.GetParameters().Length) ??
                       throw new NotSupportedException($"Unable to instantiate type without a constructor! Offending type: [{type.FullName}]");

            var args = ctor.GetParameters();
            var argExpressions = new List<Expression>(args.Length);

            for (var i = 0; i < args.Length; i++)
            {
                argExpressions.Add(
                    args[i].HasDefaultValue
                        ? Expression.Constant(args[i].DefaultValue, args[i].ParameterType)
                        : Expression.Default(args[i].ParameterType));
            }

            var ctorExpression = Expression.New(ctor, argExpressions);

            return Expression.Lambda<Func<object>>(ctorExpression).Compile();
        }
    }
}