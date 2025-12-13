// ReSharper disable InconsistentNaming

using System.Collections;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;

namespace FastEndpoints;

static class Types
{
    //there's no performance benefit gained from this.
    //it's only there to make code more readable and save a few keystrokes.

    internal static readonly Type Bool = typeof(bool);
    internal static readonly Type Byte = typeof(byte);
    internal static readonly Type DontInjectAttribute = typeof(DontInjectAttribute);
    internal static readonly Type DontRegisterAttribute = typeof(DontRegisterAttribute);
    internal static readonly Type EmptyRequest = typeof(EmptyRequest);
    internal static readonly Type EmptyResponse = typeof(EmptyResponse);
    internal static readonly Type EndpointOf2 = typeof(Endpoint<,>);
    internal static readonly Type FormFileCollection = typeof(FormFileCollection);
    internal static readonly Type FromBodyAttribute = typeof(FromBodyAttribute);
    internal static readonly Type FromFormAttribute = typeof(FromFormAttribute);
    internal static readonly Type FromHeaderAttribute = typeof(FromHeaderAttribute);
    internal static readonly Type HideFromDocsAttribute = typeof(HideFromDocsAttribute);
    internal static readonly Type Http = typeof(Http);
    internal static readonly Type ICommandHandler = typeof(ICommandHandler);
    internal static readonly Type IDictionary = typeof(IDictionary);
    internal static readonly Type IEndpoint = typeof(IEndpoint);
    internal static readonly Type IEndpointFeature = typeof(IEndpointFeature);
    internal static readonly Type IEndpointValidator = typeof(IEndpointValidator);
    internal static readonly Type IEnumerable = typeof(IEnumerable);
    internal static readonly Type IEnumerableOfIFormFile = typeof(IEnumerable<IFormFile>);
    internal static readonly Type IEventHandler = typeof(IEventHandler);
    internal static readonly Type IFormatProvider = typeof(IFormatProvider);
    internal static readonly Type IFormFile = typeof(IFormFile);
    internal static readonly Type IHasMapperOf1 = typeof(IHasMapper<>);
    internal static readonly Type IPlainTextRequest = typeof(IPlainTextRequest);
    internal static readonly Type IPreProcessorOf1 = typeof(IPreProcessor<>);
    internal static readonly Type IPostProcessorOf2 = typeof(IPostProcessor<,>);
    internal static readonly Type IRequestBinderOf1 = typeof(IRequestBinder<>);
    internal static readonly Type IResult = typeof(IResult);
    internal static readonly Type ISummary = typeof(ISummary);
    internal static readonly Type IValidator = typeof(IValidator);
    internal static readonly Type ListOf1 = typeof(List<>);
    internal static readonly Type NotImplementedAttribute = typeof(NotImplementedAttribute);
    internal static readonly Type NonJsonBindingAttribute = typeof(NonJsonBindingAttribute);
    internal static readonly Type Object = typeof(object);
    internal static readonly Type ParseResult = typeof(ParseResult);
    internal static readonly Type QueryParamAttribute = typeof(QueryParamAttribute);
    internal static readonly Type String = typeof(string);
    internal static readonly Type StringSegment = typeof(StringSegment);
    internal static readonly Type StringValues = typeof(StringValues);
    internal static readonly Type SummaryOf1 = typeof(Summary<>);
    internal static readonly Type SummaryOf2 = typeof(Summary<,>);
    internal static readonly Type ToHeaderAttribute = typeof(ToHeaderAttribute);
    internal static readonly Type ValidatorOf1 = typeof(AbstractValidator<>);
    internal static readonly Type ValidationAttribute = typeof(System.ComponentModel.DataAnnotations.ValidationAttribute);
    internal static readonly Type Void = typeof(void);
    internal static readonly Type Uri = typeof(Uri);
}