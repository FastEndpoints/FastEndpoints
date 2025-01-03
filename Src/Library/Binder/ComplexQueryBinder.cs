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
        BindPropertiesRecursively(propValue, string.Empty, queryParams, failures);
        fromQueryProp.PropSetter(requestDto, propValue);
    }

    static bool BindPropertiesRecursively(object parent, string prefix, IQueryCollection queryParams, List<ValidationFailure> failures)
    {
        var tParent = parent.GetType();
        var properties = tParent.BindableProps();
        var bound = false;

        foreach (var prop in properties)
        {
            var fieldName = prop.FieldName();
            var tProp = prop.PropertyType.GetUnderlyingType();
            var key = string.IsNullOrEmpty(prefix)
                          ? fieldName
                          : $"{prefix}.{fieldName}";

            if (tProp.IsComplexType() && !tProp.IsCollection())
                bound = BindComplexType(parent, prop, tProp, key, queryParams, failures) || bound;
            else if (tProp.IsCollection())
                bound = BindCollectionType(parent, prop, tProp, key, queryParams, failures) || bound;
            else
                bound = BindSimpleType(parent, prop, tProp, key, queryParams, failures) || bound;
        }

        return bound;

        static bool BindComplexType(object parent, PropertyInfo prop, Type tProp, string key, IQueryCollection queryParams, List<ValidationFailure> failures)
        {
            var propVal = tProp.ObjectFactory()();
            var bound = BindPropertiesRecursively(propVal, key, queryParams, failures);
            parent.GetType().SetterForProp(prop)(parent, propVal);

            return bound;
        }

        static bool BindCollectionType(object parent, PropertyInfo prop, Type tProp, string key, IQueryCollection queryParams, List<ValidationFailure> failures)
        {
            var tElement = tProp.IsGenericType
                               ? tProp.GetGenericArguments()[0]
                               : tProp.GetElementType();

            if (tElement is null)
                return false;

            var tList = Types.ListOf1.MakeGenericType(tElement);
            var list = (IList)tList.ObjectFactory()();

            if (!tProp.IsAssignableFrom(tList))
            {
                throw new NotSupportedException(
                    $"'{tProp.Name}' type properties are not supported for complex query param binding! Offender: [{parent.GetType().FullName}.{key.Replace("[0]", "")}]");
            }

            var bound = tElement.IsComplexType()
                            ? BindComplexCollection(list, tElement, key, queryParams, failures)
                            : BindSimpleCollection(list, tElement, key, queryParams, failures);

            parent.GetType().SetterForProp(prop)(parent, list);

            return bound;

            static bool BindComplexCollection(IList list, Type tElement, string key, IQueryCollection queryParams, List<ValidationFailure> failures)
            {
                var index = 0;
                var bound = false;

                while (true)
                {
                    var indexedKey = $"{key}[{index}]";
                    var item = tElement.ObjectFactory()();

                    if (BindPropertiesRecursively(item, indexedKey, queryParams, failures))
                    {
                        list.Add(item);
                        index++;
                        bound = true;
                    }
                    else
                        break;
                }

                return bound;
            }

            static bool BindSimpleCollection(IList list, Type tElement, string key, IQueryCollection queryParams, List<ValidationFailure> failures)
            {
                var bound = false;
                var index = -1;

                while (true)
                {
                    var indexedKey = index == -1
                                         ? key
                                         : $"{key}[{index}]";

                    if (!queryParams.TryGetValue(indexedKey, out var val) && index > -1)
                        break;

                    foreach (var v in val)
                    {
                        var res = tElement.ValueParser()(v);

                        if (!res.IsSuccess)
                        {
                            failures.Add(new(indexedKey, Cfg.BndOpts.FailureMessage(tElement, indexedKey, v)));

                            continue;
                        }

                        list.Add(res.Value);
                        bound = true;
                    }

                    index++;
                }

                return bound;
            }
        }

        static bool BindSimpleType(object parent, PropertyInfo prop, Type tProp, string key, IQueryCollection queryParams, List<ValidationFailure> failures)
        {
            if (!queryParams.TryGetValue(key, out var val))
                return false;

            var res = tProp.ValueParser()(val);

            if (!res.IsSuccess)
            {
                failures.Add(new(key, Cfg.BndOpts.FailureMessage(tProp, key, val)));

                return false;
            }

            parent.GetType().SetterForProp(prop)(parent, res.Value);

            return true;
        }
    }
}