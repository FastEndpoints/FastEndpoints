using System.Collections;
using System.Reflection;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

static class ComplexFormBinder
{
    internal static void Bind(PropCache fromFormProp, object requestDto, IFormCollection forms)
    {
        var propInstance = fromFormProp.PropType.ObjectFactory()();

        BindPropertiesRecursively(propInstance, forms, string.Empty);

        fromFormProp.PropSetter(requestDto, propInstance);

        static bool BindPropertiesRecursively(object obj, IFormCollection form, string prefix)
        {
            var tObject = obj.GetType();
            var properties = tObject.BindableProps();
            var bound = false;

            foreach (var prop in properties)
            {
                var propName = prop.GetCustomAttribute<BindFromAttribute>()?.Name ?? prop.Name;
                var key = string.IsNullOrEmpty(prefix)
                              ? propName
                              : $"{prefix}.{propName}";

                if (Types.IFormFile.IsAssignableFrom(prop.PropertyType))
                {
                    if (form.Files.GetFile(key) is not { } file)
                        continue;

                    tObject.SetterForProp(prop)(obj, file);
                    bound = true;
                }
                else if (Types.IEnumerableOfIFormFile.IsAssignableFrom(prop.PropertyType))
                {
                    if (!prop.PropertyType.IsAssignableFrom(Types.FormFileCollection))
                    {
                        throw new NotSupportedException(
                            $"'{prop.PropertyType.Name}' type properties are not supported for complex nested form binding! Offender: [{tObject.FullName}.{key}]");
                    }

                    var files = form.Files.GetFiles(key);

                    if (files.Count == 0)
                        continue;

                    var collection = new FormFileCollection();
                    collection.AddRange(files);
                    tObject.SetterForProp(prop)(obj, collection);
                    bound = true;
                }
                else if (prop.PropertyType.IsComplexType() && !Types.IEnumerable.IsAssignableFrom(prop.PropertyType))
                {
                    var nestedObject = prop.PropertyType.ObjectFactory()();
                    bound = BindPropertiesRecursively(nestedObject, form, key);
                    tObject.SetterForProp(prop)(obj, nestedObject);
                }
                else if (Types.IEnumerable.IsAssignableFrom(prop.PropertyType) && prop.PropertyType != Types.String)
                {
                    var tElement = prop.PropertyType.IsGenericType
                                       ? prop.PropertyType.GetGenericArguments()[0]
                                       : prop.PropertyType.GetElementType();

                    if (tElement is null)
                        continue;

                    var tList = Types.ListOf1.MakeGenericType(tElement);
                    var list = (IList)tList.ObjectFactory()();

                    if (!prop.PropertyType.IsAssignableFrom(tList))
                    {
                        throw new NotSupportedException(
                            $"'{prop.PropertyType.Name}' type properties are not supported for complex nested form binding! Offender: [{tObject.FullName}.{key.Replace("[0]", "")}]");
                    }

                    if (tElement.IsComplexType())
                    {
                        var index = 0;

                        while (true)
                        {
                            var indexedKey = $"{key}[{index}]";
                            var item = tElement.ObjectFactory()();

                            if (BindPropertiesRecursively(item, form, indexedKey))
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
                        if (form.TryGetValue(key, out var val))
                        {
                            foreach (var v in val)
                            {
                                var res = tElement.CachedValueParser()(v);

                                if (!res.IsSuccess)
                                    continue;

                                list.Add(res.Value);
                                bound = true;
                            }
                        }
                    }

                    tObject.SetterForProp(prop)(obj, list);
                }
                else
                {
                    if (!form.TryGetValue(key, out var val))
                        continue;

                    var res = prop.PropertyType.CachedValueParser()(val);

                    if (!res.IsSuccess)
                        continue;

                    tObject.SetterForProp(prop)(obj, res.Value);
                    bound = true;
                }
            }

            return bound;
        }
    }
}