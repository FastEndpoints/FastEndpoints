namespace FastEndpoints.Validation
{
    internal class ValidationFailureException : Exception
    {
        public ValidationFailureException() { }

        public ValidationFailureException(string? message) : base(message) { }

        public ValidationFailureException(string? message, Exception? innerException) : base(message, innerException) { }
    }
}
