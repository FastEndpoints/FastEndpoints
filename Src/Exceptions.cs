namespace ApiExpress
{
    public class ValidationFailureException : Exception
    {
        public ValidationFailureException() { }

        public ValidationFailureException(string? message) : base(message) { }

        public ValidationFailureException(string? message, Exception? innerException) : base(message, innerException) { }
    }
}
