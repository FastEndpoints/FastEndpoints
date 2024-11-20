#if NET9_0_OR_GREATER
    global using Lock = System.Threading.Lock;
#else
    global using Lock = object;
#endif

    using System.Runtime.CompilerServices;

    [assembly: InternalsVisibleTo("Unit.FastEndpoints")]
    [assembly: InternalsVisibleTo("Int.FastEndpoints")]