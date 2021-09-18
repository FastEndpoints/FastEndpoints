namespace ASPie
{
    public class RequestContext
    {
        public HttpContext HttpContext { get; set; }

        public RequestContext(HttpContext httpContext)
        {
            HttpContext = httpContext;
        }
    }
}
