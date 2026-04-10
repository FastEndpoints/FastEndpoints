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
public static class SystemTextJsonUtilities
{
    /// <summary>
    /// Converts System.Text.Json serializer options to Newtonsoft JSON settings.
    /// </summary>
    /// <param name="serializerOptions">The options.</param>
    /// <returns>The settings.</returns>
    public static JsonSerializerSettings ConvertJsonOptionsToNewtonsoftSettings(System.Text.Json.JsonSerializerOptions serializerOptions)
    {
        var settings = new JsonSerializerSettings
        {
            ContractResolver = new SystemTextJsonContractResolver(serializerOptions)
        };

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
            var enumNamingPolicy = (System.Text.Json.JsonNamingPolicy?)(jsonStringEnumConverter
                                   .GetType().GetRuntimeFields()
                                   .FirstOrDefault(x => x.FieldType.FullName == "System.Text.Json.JsonNamingPolicy")?
                                   .GetValue(jsonStringEnumConverter));

            return enumNamingPolicy is not null && enumNamingPolicy == System.Text.Json.JsonNamingPolicy.CamelCase;

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
            var attributes = member.GetCustomAttributes<System.Text.Json.Serialization.JsonIgnoreAttribute>(true);

            var propertyIgnored = attributes.Any(att => att.Condition == System.Text.Json.Serialization.JsonIgnoreCondition.Always);
            var hasToHeaderAttribute = member.GetCustomAttributes<FastEndpoints.ToHeaderAttribute>(true).Any();
            var hasJsonExtensionDataAttribute = member.GetCustomAttributes<System.Text.Json.Serialization.JsonExtensionDataAttribute>().Any();

            var property = base.CreateProperty(member, memberSerialization);

            property.Ignored = propertyIgnored || hasJsonExtensionDataAttribute || hasToHeaderAttribute;

            if (serializerOptions.PropertyNamingPolicy != null)
                property.PropertyName = serializerOptions.PropertyNamingPolicy.ConvertName(member.Name);

            var jsonPropertyNameAttribute = member.GetCustomAttributes<System.Text.Json.Serialization.JsonPropertyNameAttribute>(true).FirstOrDefault();

            if (jsonPropertyNameAttribute is not null && !string.IsNullOrEmpty(jsonPropertyNameAttribute.Name))
                property.PropertyName = jsonPropertyNameAttribute?.Name;

            return property;
        }
    }
}