using System.Reflection;

namespace ASPie
{
    public abstract class RequestBase<TRequest> : IRequest
    {
        //public static ValueTask<TRequest?> BindAsync(HttpContext ctx, ParameterInfo _)
        //{
        //    return ctx.Request.ReadFromJsonAsync<TRequest>();
        //}
    }
}
