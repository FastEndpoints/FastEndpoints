using FastEndpoints.Validation;
#pragma warning disable CS8618

namespace $fileinputname$
{
    public class Request
    {

    }

    public class Validator : Validator<Request>
    {
        public Validator()
        {

        }
    }

    public class Response
    {
        public string Message => "This endpoint hasn't been implemented yet!";
    }
}