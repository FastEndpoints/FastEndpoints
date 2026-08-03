using System.Collections;
using System.Reflection;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace FastEndpoints;

static class ComplexSourceBinder
{
    internal static void Bind(PropCache propCache, object requestDto, IFormCollection form, List<ValidationFailure> failures)
        => Bind(propCache, requestDto, new FormValueSource(form), failures);

    internal static void Bind(PropCache propCache, object requestDto, IQueryCollection query, List<ValidationFailure> failures)
        => Bind(propCache, requestDto, new QueryValueSource(query), failures);

    /// <summary>Builds <c>prefix.fieldName</c>, or returns <paramref name="fieldName"/> when prefix is empty (no alloc).</summary>
    static string NestedKey(string prefix, string fieldName)
        => prefix.Length == 0
               ? fieldName
               : string.Concat(prefix, ".", fieldName);

    /// <summary>Builds <c>key[index]</c> with a single result string (no intermediate format allocations).</summary>
    static string IndexedKey(string key, int index)
    {
        Span<char> digits = stackalloc char[11]; // enough for int.MinValue
        index.TryFormat(digits, out var digitCount);

        return string.Create(
            key.Length + 2 + digitCount,
            (key, index, digitCount),
            static (span, state) =>
            {
                state.key.AsSpan().CopyTo(span);
                var pos = state.key.Length;
                span[pos++] = '[';
                state.index.TryFormat(span[pos..], out _);
                span[pos + state.digitCount] = ']';
            });
    }

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
            var meta = tParent.ComplexBindMeta(prop);
            var fieldName = meta.FieldName!;
            var key = NestedKey(prefix, fieldName);

            if (source.SupportsFiles && meta.IsFormFile)
                bound = BindFormFileProp(parent, meta, key, source) || bound;
            else if (source.SupportsFiles && meta.IsFormFileCollection)
                bound = BindFormFileCollectionProp(parent, tParent, prop, meta, key, source) || bound;
            else if (meta.IsComplex && !meta.IsCollection)
                bound = BindComplexType(parent, meta, key, source, failures) || bound;
            else if (meta.IsCollection)
                bound = BindCollectionType(parent, tParent, meta, key, source, failures) || bound;
            else
                bound = BindSimpleType(parent, meta, key, source, failures) || bound;
        }

        return bound;

        static bool BindFormFileProp(object parent, PropertyDefinition meta, string key, TSource source)
        {
            if (source.GetFile(key) is not { } file)
                return false;

            meta.Setter!(parent, file);

            return true;
        }

        static bool BindFormFileCollectionProp(object parent,
                                               Type tParent,
                                               PropertyInfo prop,
                                               PropertyDefinition meta,
                                               string key,
                                               TSource source)
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
                                     : IndexedKey(key, index);

                var files = source.GetFiles(indexedKey);

                if (files.Count == 0 && index > -1)
                    break;

                collection.AddRange(files);
                index++;
            }

            if (collection.Count == 0)
                return false;

            meta.Setter!(parent, collection);

            return true;
        }

        static bool BindComplexType(object parent,
                                    PropertyDefinition meta,
                                    string key,
                                    TSource source,
                                    List<ValidationFailure> failures)
        {
            if (!source.HasDataForPrefix(key))
                return false;

            var propVal = meta.UnderlyingType!.ObjectFactory()();
            var bound = BindPropertiesRecursively(propVal, key, source, failures);
            meta.Setter!(parent, propVal);

            return bound;
        }

        static bool BindCollectionType(object parent,
                                       Type tParent,
                                       PropertyDefinition meta,
                                       string key,
                                       TSource source,
                                       List<ValidationFailure> failures)
        {
            var tElement = meta.ElementType;

            // non-generic / unresolvable element type: same silent skip as the pre-cache binder
            if (tElement is null)
                return false;

            // element known but List<T> is not assignable to the property (e.g. T[]): fail like the original check
            if (meta.ListFactory is null)
            {
                throw new NotSupportedException(
                    $"'{meta.UnderlyingType!.Name}' type properties are not supported for complex {source.SourceName} binding! Offender: " +
                    $"[{tParent.FullName}.{key.Replace("[0]", "")}]");
            }

            // nested collections (e.g. List<List<T>>) have no key convention here - BindComplexCollection binds each
            // element as an object with named properties, never as another collection. fail loudly rather than
            // silently binding an empty list. simple-parsed elements (byte[] etc.) don't take that path.
            if (meta is { ElementIsCollection: true, ElementIsComplex: true })
            {
                throw new NotSupportedException(
                    $"Collection elements of type '{tElement.Name}' are not supported for complex {source.SourceName} binding, because they are themselves collections! Offender: [{tParent.FullName}.{key.Replace("[0]", "")}]");
            }

            var list = (IList)meta.ListFactory();

            var bound = meta.ElementIsComplex
                            ? BindComplexCollection(list, tElement, key, source, failures)
                            : BindSimpleCollection(list, tElement, meta.ValueParser!, key, source, failures);

            meta.Setter!(parent, list);

            return bound;

            static bool BindComplexCollection(IList list, Type tElement, string key, TSource source, List<ValidationFailure> failures)
            {
                var index = 0;
                var bound = false;

                while (true)
                {
                    var indexedKey = IndexedKey(key, index);

                    // No child keys under items[i] → end of collection (skip empty element alloc/walk).
                    if (!source.HasDataForPrefix(indexedKey))
                        break;

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

            static bool BindSimpleCollection(IList list,
                                             Type tElement,
                                             Func<StringValues, ParseResult> parser,
                                             string key,
                                             TSource source,
                                             List<ValidationFailure> failures)
            {
                var bound = false;
                var index = -1;

                while (true)
                {
                    var indexedKey = index == -1
                                         ? key
                                         : IndexedKey(key, index);

                    if (!source.TryGetValues(indexedKey, out var val) && index > -1)
                        break;

                    foreach (var v in val)
                    {
                        var res = parser(v);

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
                                   PropertyDefinition meta,
                                   string key,
                                   TSource source,
                                   List<ValidationFailure> failures)
        {
            if (!source.TryGetValues(key, out var val))
                return false;

            var res = meta.ValueParser!(val);

            if (!res.IsSuccess)
            {
                failures.Add(new(key, Cfg.BndOpts.FailureMessage(meta.UnderlyingType!, key, val)));

                return false;
            }

            meta.Setter!(parent, res.Value);

            return true;
        }
    }
}
