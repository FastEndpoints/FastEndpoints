using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

static class ComplexSourceBinder
{
    internal static void Bind(PropCache propCache, object requestDto, IFormCollection form, List<ValidationFailure> failures)
        => Bind(propCache, requestDto, new FormValueSource(form), failures);

    internal static void Bind(PropCache propCache, object requestDto, IQueryCollection query, List<ValidationFailure> failures)
        => Bind(propCache, requestDto, new QueryValueSource(query), failures);

    static void Bind<TSource>(PropCache propCache, object requestDto, TSource source, List<ValidationFailure> failures)
        where TSource : IPrefixedValueSource
    {
        var propValue = propCache.PropType.ObjectFactory()();
        BindPropertiesRecursively(propValue, string.Empty, source, failures);
        propCache.PropSetter(requestDto, propValue);
    }

    static bool BindPropertiesRecursively<TSource>(object parent, string prefix, TSource source, List<ValidationFailure> failures)
        where TSource : IPrefixedValueSource
    {
        var tParent = parent.GetType();
        var properties = tParent.BindableProps();
        var bound = false;

        foreach (var prop in properties)
        {
            var tProp = prop.PropertyType.GetUnderlyingType();
            var fieldName = prop.FieldName();
            var key = string.IsNullOrEmpty(prefix)
                          ? fieldName
                          : $"{prefix}.{fieldName}";

            if (source.SupportsFiles && tProp.IsFormFileProp())
                bound = BindFormFileProp(parent, tParent, prop, key, source) || bound;
            else if (source.SupportsFiles && tProp.IsFormFileCollectionProp())
                bound = BindFormFileCollectionProp(parent, tParent, prop, key, source) || bound;
            else if (tProp.IsComplexType() && !tProp.IsCollection())
                bound = BindComplexType(parent, tParent, prop, tProp, key, source, failures) || bound;
            else if (tProp.IsCollection())
                bound = BindCollectionType(parent, tParent, prop, tProp, key, source, failures) || bound;
            else
                bound = BindSimpleType(parent, tParent, prop, tProp, key, source, failures) || bound;
        }

        return bound;

        static bool BindFormFileProp(object parent, Type tParent, PropertyInfo prop, string key, TSource source)
        {
            if (source.GetFile(key) is not { } file)
                return false;

            tParent.SetterForProp(prop)(parent, file);

            return true;
        }

        static bool BindFormFileCollectionProp(object parent, Type tParent, PropertyInfo prop, string key, TSource source)
        {
            if (!prop.PropertyType.IsAssignableFrom(Types.FormFileCollection))
            {
                throw new NotSupportedException(
                    $"'{prop.PropertyType.Name}' type properties are not supported for complex {source.SourceName} binding! " +
                    $"Offender: [{tParent.FullName}.{key}]");
            }

            var collection = new FormFileCollection();
            var index = -1;

            while (true)
            {
                var indexedKey = index == -1
                                     ? key
                                     : $"{key}[{index}]";

                var files = source.GetFiles(indexedKey);

                if (files.Count == 0 && index > -1)
                    break;

                collection.AddRange(files);
                index++;
            }

            if (collection.Count == 0)
                return false;

            tParent.SetterForProp(prop)(parent, collection);

            return true;
        }

        static bool BindComplexType(object parent,
                                    Type tParent,
                                    PropertyInfo prop,
                                    Type tProp,
                                    string key,
                                    TSource source,
                                    List<ValidationFailure> failures)
        {
            if (!source.HasDataForPrefix(key))
                return false;

            var propVal = tProp.ObjectFactory()();
            var bound = BindPropertiesRecursively(propVal, key, source, failures);
            tParent.SetterForProp(prop)(parent, propVal);

            return bound;
        }

        [UnconditionalSuppressMessage("aot", "IL2055"), UnconditionalSuppressMessage("aot", "IL3050")]
        static bool BindCollectionType(object parent,
                                       Type tParent,
                                       PropertyInfo prop,
                                       Type tProp,
                                       string key,
                                       TSource source,
                                       List<ValidationFailure> failures)
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
                    $"'{tProp.Name}' type properties are not supported for complex {source.SourceName} binding! Offender: " +
                    $"[{tParent.FullName}.{key.Replace("[0]", "")}]");
            }

            var bound = tElement.IsComplexType()
                            ? BindComplexCollection(list, tElement, key, source, failures)
                            : BindSimpleCollection(list, tElement, key, source, failures);

            tParent.SetterForProp(prop)(parent, list);

            return bound;

            static bool BindComplexCollection(IList list, Type tElement, string key, TSource source, List<ValidationFailure> failures)
            {
                var index = 0;
                var bound = false;

                while (true)
                {
                    var indexedKey = $"{key}[{index}]";
                    var item = tElement.ObjectFactory()();

                    if (BindPropertiesRecursively(item, indexedKey, source, failures))
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

            static bool BindSimpleCollection(IList list, Type tElement, string key, TSource source, List<ValidationFailure> failures)
            {
                var bound = false;
                var index = -1;

                while (true)
                {
                    var indexedKey = index == -1
                                         ? key
                                         : $"{key}[{index}]";

                    if (!source.TryGetValues(indexedKey, out var val) && index > -1)
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

        static bool BindSimpleType(object parent,
                                   Type tParent,
                                   PropertyInfo prop,
                                   Type tProp,
                                   string key,
                                   TSource source,
                                   List<ValidationFailure> failures)
        {
            if (!source.TryGetValues(key, out var val))
                return false;

            var res = tProp.ValueParser()(val);

            if (!res.IsSuccess)
            {
                failures.Add(new(key, Cfg.BndOpts.FailureMessage(tProp, key, val)));

                return false;
            }

            tParent.SetterForProp(prop)(parent, res.Value);

            return true;
        }
    }
}
