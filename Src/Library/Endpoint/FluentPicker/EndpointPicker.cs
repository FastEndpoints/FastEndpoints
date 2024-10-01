namespace FastEndpoints;

/// <summary>
/// endpoint base class picker starting point
/// </summary>
public static class Ep
{
    /// <summary>
    /// specify the request dto type of the endpoint
    /// </summary>
    /// <typeparam name="TRequest">type of the request dto</typeparam>
    public static class Req<TRequest> where TRequest : notnull
    {
        /// <summary>
        /// specify the response dto type of the endpoint
        /// </summary>
        /// <typeparam name="TResponse">the type of the response dto</typeparam>
        public abstract class Res<TResponse> : Endpoint<TRequest, TResponse> where TResponse : notnull
        {
            /// <summary>
            /// specify the mapper type of the endpoint
            /// </summary>
            /// <typeparam name="TMapper">the type of the mapper for the endpoint</typeparam>
            public abstract class Map<TMapper> : Endpoint<TRequest, TResponse, TMapper> where TMapper : class, IMapper;
        }

        /// <summary>
        /// specifies that the endpoint has no response dto
        /// </summary>
        public abstract class NoRes : Endpoint<TRequest>
        {
            /// <summary>
            /// specify the request mapper type of the endpoint
            /// </summary>
            /// <typeparam name="TMapper">the type of the mapper for the endpoint</typeparam>
            public abstract class Map<TMapper> : EndpointWithMapper<TRequest, TMapper> where TMapper : class, IRequestMapper;
        }
    }

    /// <summary>
    /// specifies that the endpoint has no request dto
    /// </summary>
    public static class NoReq
    {
        /// <summary>
        /// specifies that the endpoint has no response dto
        /// </summary>
        public abstract class NoRes : EndpointWithoutRequest;

        /// <summary>
        /// specify the response dto type of the endpoint
        /// </summary>
        /// <typeparam name="TResponse">the type of the response dto</typeparam>
        public abstract class Res<TResponse> : EndpointWithoutRequest<TResponse> where TResponse : notnull
        {
            /// <summary>
            /// specify the mapper type of the endpoint
            /// </summary>
            /// <typeparam name="TMapper">the type of the mapper for the endpoint</typeparam>
            public abstract class Map<TMapper> : EndpointWithoutRequest<TResponse, TMapper> where TMapper : class, IResponseMapper;
        }
    }
}