#pragma warning disable RCS1074
    namespace FastEndpoints;

    /// <summary>
    /// a request dto that doesn't have any properties
    /// </summary>
    public sealed class EmptyRequest
    {
        /// <summary>
        /// a cached empty request instance.
        /// </summary>
        public static EmptyRequest Instance { get; } = new();

        //private ctor only used by above cached instance.
        EmptyRequest() { }
    }