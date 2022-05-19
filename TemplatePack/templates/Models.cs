#if (useValidator)
using FastEndpoints;

#endif
namespace FeatureName;

public class Request
{

}
#if (useValidator)

public class Validator : Validator<Request>
{
    public Validator()
    {
        
    }
}
#endif

public class Response
{
    public string Message => "This endpoint hasn't been implemented yet!";
}
