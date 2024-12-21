using System.Collections;
using System.Reflection;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

static class ComplexQueryBinder
{
    internal static void Bind(PropCache fromQueryProp, object requestDto, IQueryCollection queryParams, List<ValidationFailure> failures)
    {
        var propValue = fromQueryProp.PropType.ObjectFactory()();

        BindPropertiesRecursively(propValue, queryParams, string.Empty, failures);

        fromQueryProp.PropSetter(requestDto, propValue);

        static bool BindPropertiesRecursively(object obj, IQueryCollection queryParams, string prefix, List<ValidationFailure> failures)
        {
            var tObject = obj.GetType();
            var properties = tObject.BindableProps();
            var bound = false;

            foreach (var prop in properties)
            {
                var tProp = prop.PropertyType.GetUnderlyingType();
                var propName = prop.GetCustomAttribute<BindFromAttribute>()?.Name ?? prop.Name;
                var key = string.IsNullOrEmpty(prefix)
                              ? propName
                              : $"{prefix}.{propName}";

                if (tProp.IsComplexType() && !tProp.IsCollection())
                {
                    var propVal = tProp.ObjectFactory()();
                    bound = BindPropertiesRecursively(propVal, queryParams, key, failures);
                    tObject.SetterForProp(prop)(obj, propVal);
                }
                else if (tProp.IsCollection())
                {
                    var tElement = tProp.IsGenericType
                                       ? tProp.GetGenericArguments()[0]
                                       : tProp.GetElementType();

                    if (tElement is null)
                        continue;

                    var tList = Types.ListOf1.MakeGenericType(tElement);
                    var list = (IList)tList.ObjectFactory()();

                    if (!tProp.IsAssignableFrom(tList))
                    {
                        throw new NotSupportedException(
                            $"'{tProp.Name}' type properties are not supported for complex query param binding! Offender: " +
                            $"[{tObject.FullName}.{key.Replace("[0]", "")}]");
                    }

                    if (tElement.IsComplexType())
                    {
                        var index = 0;

                        while (true)
                        {
                            var indexedKey = $"{key}[{index}]";
                            var item = tElement.ObjectFactory()();

                            if (BindPropertiesRecursively(item, queryParams, indexedKey, failures))
                            {
                                list.Add(item);
                                index++;
                                bound = true;
                            }
                            else
                                break;
                        }
                    }
                    else
                    {
                        if (queryParams.TryGetValue(key, out var val))
                        {
                            foreach (var v in val)
                            {
                                var res = tElement.CachedValueParser()(v);

                                if (!res.IsSuccess)
                                {
                                    failures.Add(new(key, Cfg.BndOpts.FailureMessage(tElement, key, v)));

                                    continue;
                                }

                                list.Add(res.Value);
                                bound = true;
                            }
                        }
                    }

                    tObject.SetterForProp(prop)(obj, list);
                }
                else
                {
                    if (!queryParams.TryGetValue(key, out var val))
                        continue;

                    var res = tProp.CachedValueParser()(val);

                    if (!res.IsSuccess)
                    {
                        failures.Add(new(key, Cfg.BndOpts.FailureMessage(tProp, key, val)));

                        continue;
                    }

                    tObject.SetterForProp(prop)(obj, res.Value);
                    bound = true;
                }
            }

            return bound;
        }
    }
}