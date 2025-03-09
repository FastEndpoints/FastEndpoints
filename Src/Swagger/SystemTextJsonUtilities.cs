//-----------------------------------------------------------------------
// <copyright file="SystemTextJsonUtilities.cs" company="NJsonSchema">
//     Copyright (c) Rico Suter. All rights reserved.
// </copyright>
// <license>https://github.com/RicoSuter/NJsonSchema/blob/master/LICENSE.md</license>
// <author>Rico Suter, mail@rsuter.com</author>
//-----------------------------------------------------------------------

using Namotion.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System.Collections;
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
    public static JsonSerializerSettings ConvertJsonOptionsToNewtonsoftSettings(dynamic serializerOptions)
    {
        var settings = new JsonSerializerSettings
        {
            ContractResolver = new SystemTextJsonContractResolver(serializerOptions)
        };

        var jsonStringEnumConverter = ((IEnumerable)serializerOptions.Converters)
                                      .OfType<object>()
                                      .FirstOrDefault(
                                          c => c.GetType()
                                                .IsAssignableToTypeName("System.Text.Json.Serialization.JsonStringEnumConverter", TypeNameStyle.FullName));

        if (jsonStringEnumConverter == null)
            return settings;

        var camelCasePolicy = IsCamelCaseEnumNamingPolicy(jsonStringEnumConverter);
        settings.Converters.Add(new StringEnumConverter(camelCasePolicy ? new CamelCaseNamingStrategy() : null!));

        return settings;
    }

    static bool IsCamelCaseEnumNamingPolicy(object jsonStringEnumConverter)
    {
        try
        {
            var enumNamingPolicy = jsonStringEnumConverter
                                   .GetType().GetRuntimeFields()
                                   .FirstOrDefault(x => x.FieldType.FullName == "System.Text.Json.JsonNamingPolicy")?
                                   .GetValue(jsonStringEnumConverter);

            return enumNamingPolicy is not null && enumNamingPolicy.GetType().FullName == "System.Text.Json.JsonCamelCaseNamingPolicy";
        }
        catch
        {
            return false;
        }
    }

    sealed class SystemTextJsonContractResolver(dynamic serializerOptions) : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var attributes = member.GetCustomAttributes(true);

            var property = base.CreateProperty(member, memberSerialization);

            var propertyIgnored = false;
            var jsonIgnoreAttribute = attributes.FirstAssignableToTypeNameOrDefault("System.Text.Json.Serialization.JsonIgnoreAttribute");

            if (jsonIgnoreAttribute != null)
            {
                var condition = jsonIgnoreAttribute.TryGetPropertyValue<object>("Condition");
                if (condition is null || condition.ToString() == "Always")
                    propertyIgnored = true;
            }

            var hasToHeaderAttribute = attributes.FirstAssignableToTypeNameOrDefault("FastEndpoints.ToHeaderAttribute") is not null;
            var hasJsonExtensionDataAttribute = attributes.FirstAssignableToTypeNameOrDefault("System.Text.Json.Serialization.JsonExtensionDataAttribute") is not null;

            property.Ignored = propertyIgnored || hasJsonExtensionDataAttribute || hasToHeaderAttribute;

            if (serializerOptions.PropertyNamingPolicy != null)
                property.PropertyName = serializerOptions.PropertyNamingPolicy.ConvertName(member.Name);

            dynamic? jsonPropertyNameAttribute = attributes.FirstAssignableToTypeNameOrDefault("System.Text.Json.Serialization.JsonPropertyNameAttribute");

            if (jsonPropertyNameAttribute is not null && !string.IsNullOrEmpty(jsonPropertyNameAttribute.Name))
                property.PropertyName = jsonPropertyNameAttribute?.Name;

            return property;
        }
    }
}