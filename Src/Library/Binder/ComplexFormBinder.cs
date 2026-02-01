using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

static class ComplexFormBinder
{
    internal static void Bind(PropCache fromFormProp, object requestDto, IFormCollection forms, List<ValidationFailure> failures)
    {
        var propValue = fromFormProp.PropType.ObjectFactory()();
        BindPropertiesRecursively(propValue, string.Empty, forms, failures);
        fromFormProp.PropSetter(requestDto, propValue);
    }

    static bool BindPropertiesRecursively(object parent, string prefix, IFormCollection form, List<ValidationFailure> failures)
    {
        var tObject = parent.GetType();
        var properties = tObject.BindableProps();
        var bound = false;

        foreach (var prop in properties)
        {
            var tProp = prop.PropertyType.GetUnderlyingType();
            var fieldName = prop.FieldName();
            var key = string.IsNullOrEmpty(prefix) ? fieldName : $"{prefix}.{fieldName}";

            if (tProp.IsFormFileProp())
                bound = BindFormFileProp(parent, tObject, prop, key, form) || bound;
            else if (tProp.IsFormFileCollectionProp())
                bound = BindFormFileCollectionProp(parent, tObject, prop, key, form) || bound;
            else if (tProp.IsComplexType() && !tProp.IsCollection())
                bound = BindComplexType(parent, tObject, prop, tProp, key, form, failures) || bound;
            else if (tProp.IsCollection())
                bound = BindCollectionType(parent, tObject, prop, tProp, key, form, failures) || bound;
            else
                bound = BindSimpleType(parent, tObject, prop, tProp, key, form, failures) || bound;
        }

        return bound;

        static bool BindFormFileProp(object parent, Type tParent, PropertyInfo prop, string key, IFormCollection form)
        {
            if (form.Files.GetFile(key) is not { } file)
                return false;

            tParent.SetterForProp(prop)(parent, file);

            return true;
        }

        static bool BindFormFileCollectionProp(object parent, Type tParent, PropertyInfo prop, string key, IFormCollection form)
        {
            if (!prop.PropertyType.IsAssignableFrom(Types.FormFileCollection))
            {
                throw new NotSupportedException(
                    $"'{prop.PropertyType.Name}' type properties are not supported for complex form binding! " +
                    $"Offender: [{tParent.FullName}.{key}]");
            }

            var collection = new FormFileCollection();
            var index = -1;

            while (true)
            {
                var indexedKey = index == -1
                                     ? key
                                     : $"{key}[{index}]";

                var files = form.Files.GetFiles(indexedKey);

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

        static bool BindComplexType(object parent, Type tParent, PropertyInfo prop, Type tProp, string key, IFormCollection form, List<ValidationFailure> failures)
        {
            var propVal = tProp.ObjectFactory()();
            var bound = BindPropertiesRecursively(propVal, key, form, failures);
            tParent.SetterForProp(prop)(parent, propVal);

            return bound;
        }

        [UnconditionalSuppressMessage("aot", "IL2055"), UnconditionalSuppressMessage("aot", "IL3050")]
        static bool BindCollectionType(object parent,
                                       Type tParent,
                                       PropertyInfo prop,
                                       Type tProp,
                                       string key,
                                       IFormCollection form,
                                       List<ValidationFailure> failures)
        {
            var tElement = tProp.IsGenericType ? tProp.GetGenericArguments()[0] : tProp.GetElementType();

            if (tElement is null)
                return false;

            var tList = Types.ListOf1.MakeGenericType(tElement);
            var list = (IList)tList.ObjectFactory()();

            if (!tProp.IsAssignableFrom(tList))
            {
                throw new NotSupportedException(
                    $"'{tProp.Name}' type properties are not supported for complex form binding! Offender: " +
                    $"[{tParent.FullName}.{key.Replace("[0]", "")}]");
            }

            var bound = tElement.IsComplexType()
                            ? BindComplexCollection(list, tElement, key, form, failures)
                            : BindSimpleCollection(list, tElement, key, form, failures);

            tParent.SetterForProp(prop)(parent, list);

            return bound;

            static bool BindComplexCollection(IList list, Type tElement, string key, IFormCollection form, List<ValidationFailure> failures)
            {
                var index = 0;
                var bound = false;

                while (true)
                {
                    var indexedKey = $"{key}[{index}]";
                    var item = tElement.ObjectFactory()();

                    if (BindPropertiesRecursively(item, indexedKey, form, failures))
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

            static bool BindSimpleCollection(IList list, Type tElement, string key, IFormCollection form, List<ValidationFailure> failures)
            {
                var bound = false;
                var index = -1;

                while (true)
                {
                    var indexedKey = index == -1
                                         ? key
                                         : $"{key}[{index}]";

                    if (!form.TryGetValue(indexedKey, out var val) && index > -1)
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

        static bool BindSimpleType(object parent, Type tParent, PropertyInfo prop, Type tProp, string key, IFormCollection form, List<ValidationFailure> failures)
        {
            if (!form.TryGetValue(key, out var val))
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