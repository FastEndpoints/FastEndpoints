// var bld = WebApplication.CreateBuilder(args);
// bld.Services
//    .SwaggerDocument()
//    .AddFastEndpoints();
//
// var app = bld.Build();
// app.UseFastEndpoints()
//    .UseSwaggerGen();
// app.Run();

//public partial class Program;

using System.Globalization;

Func<object?, ParseResult> func = input => new(GId.TryParse(input?.ToString(), CultureInfo.InvariantCulture, out var result), result);

sealed class GId : IParsable<GId?>
{
    public Guid Id { get; init; }

    public static GId Parse(string s, IFormatProvider? provider)
        => throw new NotImplementedException();

    public static bool TryParse(string? s, IFormatProvider? provider, out GId? result)
    {
        if (Guid.TryParse(s, provider, out var res))
        {
            result = new() { Id = res };

            return true;
        }

        result = null;

        return false;
    }
}