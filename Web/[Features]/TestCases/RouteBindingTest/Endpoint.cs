using FastEndpoints;
using FastEndpoints.Validation;

namespace TestCases.RouteBindingTest
{
    public class Validator : Validator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.FromBody).Must(x => x != "xxx");
        }
    }

    public class Endpoint : Endpoint<Request>
    {
        public Endpoint()
        {
            Verbs(Http.POST);
            Routes("/test-cases/route-binding-test/{string}/{bool}/{int}/{long}/{double}/{decimal}");
            AllowAnnonymous();
            DontThrowIfValidationFails(); //FromBody value will cause validation failure but will not auto throw
        }

        protected override Task HandleAsync(Request r, CancellationToken t)
        {
            return SendAsync(new Response
            {
                Bool = r.Bool,
                Decimal = r.Decimal,
                Double = r.Double,
                FromBody = r.FromBody,
                Int = r.Int,
                Long = r.Long,
                String = r.String,
            });
        }
    }
}