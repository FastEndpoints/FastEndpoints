//using Microsoft.OpenApi.Models;
//using Swashbuckle.AspNetCore.SwaggerGen;
//using System.Reflection;

//namespace FastEndpoints.Swashbuckle;

//public class DefaultSchemaFilter : ISchemaFilter
//{
//    public void Apply(OpenApiSchema schema, SchemaFilterContext ctx)
//    {
//        if (schema?.Properties is null)
//            return;

//        var excludedProps = ctx.Type
//            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
//            .Where(p =>
//                   p.GetCustomAttribute<FromClaimAttribute>()?.IsRequired is true ||
//                   p.GetCustomAttribute<FromHeaderAttribute>()?.IsRequired is true);

//        foreach (var prop in excludedProps)
//        {

//            if (schema.Properties.ContainsKey(prop.Name))
//                schema.Properties.Remove(prop.Name);
//        }
//    }
//}
