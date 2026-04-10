//-----------------------------------------------------------------------
// <copyright file="SystemTextJsonUtilities.cs" company="NJsonSchema">
//     Copyright (c) Rico Suter. All rights reserved.
// </copyright>
// <license>https://github.com/RicoSuter/NJsonSchema/blob/master/LICENSE.md</license>
// <author>Rico Suter, mail@rsuter.com</author>
//-----------------------------------------------------------------------

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System.Reflection;

namespace NJsonSchema.Generation;

/// <summary>
/// Utility methods for dealing with System.Text.Json.
/// </summary>
static class SystemTextJsonUtilities
{
    /// <summary>
    /// Converts System.Text.Json serializer options to Newtonsoft JSON settings.
    /// </summary>
    /// <param name="serializerOptions">The options.</param>
    /// <returns>The settings.</returns>
    public static JsonSerializerSettings ConvertJsonOptionsToNewtonsoftSettings(System.Text.Json.JsonSerializerOptions serializerOptions)
    {
        var settings = new JsonSerializerSettings { ContractResolver = new SystemTextJsonContractResolver(serializerOptions) };
        var jsonStringEnumConverter = serializerOptions.Converters.OfType<System.Text.Json.Serialization.JsonStringEnumConverter>().FirstOrDefault();

        if (jsonStringEnumConverter == null)
            return settings;

        var camelCasePolicy = IsCamelCaseEnumNamingPolicy(jsonStringEnumConverter);
        settings.Converters.Add(new StringEnumConverter(camelCasePolicy ? new CamelCaseNamingStrategy() : null!));

        return settings;
    }

    static bool IsCamelCaseEnumNamingPolicy(System.Text.Json.Serialization.JsonStringEnumConverter jsonStringEnumConverter)
    {
        try
        {
            var enumNamingPolicy = jsonStringEnumConverter.GetType()
                                                          .GetRuntimeFields()
                                                          .FirstOrDefault(x => x.FieldType == typeof(System.Text.Json.JsonNamingPolicy))?
                                                          .GetValue(jsonStringEnumConverter) as System.Text.Json.JsonNamingPolicy;

            return enumNamingPolicy == System.Text.Json.JsonNamingPolicy.CamelCase;
        }
        catch
        {
            return false;
        }
    }

    sealed class SystemTextJsonContractResolver(System.Text.Json.JsonSerializerOptions serializerOptions) : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);

            property.Ignored |=
                member.GetCustomAttribute<System.Text.Json.Serialization.JsonIgnoreAttribute>(true)?.Condition == System.Text.Json.Serialization.JsonIgnoreCondition.Always ||
                member.IsDefined(typeof(System.Text.Json.Serialization.JsonExtensionDataAttribute), true) ||
                member.IsDefined(typeof(FastEndpoints.ToHeaderAttribute), true);

            if (serializerOptions.PropertyNamingPolicy != null)
                property.PropertyName = serializerOptions.PropertyNamingPolicy.ConvertName(member.Name);

            if (member.GetCustomAttribute<System.Text.Json.Serialization.JsonPropertyNameAttribute>(true) is { Name: { Length: > 0 } name })
                property.PropertyName = name;

            return property;
        }
    }
}