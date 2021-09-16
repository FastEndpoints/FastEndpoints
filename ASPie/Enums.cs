namespace ASPie
{
    [Flags]
    public enum Http
    {
        GET = 1 << 1,
        POST = 1 << 2,
        PUT = 1 << 3,
        PATCH = 1 << 4,
        DELETE = 1 << 5
    }
}
