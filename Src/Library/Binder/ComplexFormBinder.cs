using System.Collections;
using System.Reflection;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

//TODO: optimize this abomination!!!

static class ComplexFormBinder
{
    internal static void Bind(PropCache fromFormProp, object requestDto, IFormCollection forms)
    {
        var propInstance = Activator.CreateInstance(fromFormProp.PropType);

        if (propInstance is null)
            return;

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

            if (typeof(IFormFile).IsAssignableFrom(prop.PropertyType))
            {
                if (form.Files.GetFile(key) is { } file)
                    prop.SetValue(obj, file);
            }
            else if (typeof(IEnumerable<IFormFile>).IsAssignableFrom(prop.PropertyType))
            {
                var files = form.Files.GetFiles(key);

                if (files.Count == 0)
                    continue;

                var collection = new FormFileCollection();
                collection.AddRange(files);
                prop.SetValue(obj, collection);
            }
            else if (prop.PropertyType.IsClass && prop.PropertyType != typeof(string) && !typeof(IEnumerable).IsAssignableFrom(prop.PropertyType))
            {
                var nestedObject = Activator.CreateInstance(prop.PropertyType);

                if (nestedObject is null)
                    continue;

                BindPropertiesRecursively(nestedObject, form, key);
                prop.SetValue(obj, nestedObject);
            }
            else if (typeof(IEnumerable).IsAssignableFrom(prop.PropertyType) && prop.PropertyType != typeof(string))
            {
                var tElement = prop.PropertyType.IsGenericType
                                   ? prop.PropertyType.GetGenericArguments()[0]
                                   : prop.PropertyType.GetElementType();

                if (tElement is null)
                    continue;

                var list = (IList?)Activator.CreateInstance(typeof(List<>).MakeGenericType(tElement));

                if (list is null)
                    continue;

                var index = 0;

                while (true)
                {
                    var indexedKey = $"{key}[{index}]";
                    var item = Activator.CreateInstance(tElement);

                    if (item is null)
                        break;

                    BindPropertiesRecursively(item, form, indexedKey);

                    if (HasAnyPropertySet(item))
                    {
                        list?.Add(item);
                        index++;
                    }
                    else
                        break;

                    static bool HasAnyPropertySet(object? obj)
                    {
                        return obj?.GetType().BindableProps().Any(
                                   p =>
                                   {
                                       var val = p.GetValue(obj);

                                       if (val is IEnumerable enm)
                                           return enm.Cast<object>().Any();

                                       return val is not null && HasAnyPropertySet(val);
                                   }) ??
                               false;
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
}