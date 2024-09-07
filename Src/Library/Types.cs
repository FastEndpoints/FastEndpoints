﻿// ReSharper disable InconsistentNaming

using System.Collections;
using System.Text.Json.Serialization;
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
    internal static readonly Type CommandHandlerExecutorOf1 = typeof(CommandHandlerExecutor<>);
    internal static readonly Type CommandHandlerExecutorOf2 = typeof(CommandHandlerExecutor<,>);
    internal static readonly Type DontInjectAttribute = typeof(DontInjectAttribute);
    internal static readonly Type DontRegisterAttribute = typeof(DontRegisterAttribute);
    internal static readonly Type EmptyRequest = typeof(EmptyRequest);
    internal static readonly Type EmptyResponse = typeof(EmptyResponse);
    internal static readonly Type EndpointOf2 = typeof(Endpoint<,>);
    internal static readonly Type EndpointOf3 = typeof(Endpoint<,,>);
    internal static readonly Type EndpointWithMapperOf2 = typeof(EndpointWithMapper<,>);
    internal static readonly Type EndpointWithOutRequestOf2 = typeof(EndpointWithoutRequest<,>);
    internal static readonly Type FromBodyAttribute = typeof(FromBodyAttribute);
    internal static readonly Type FromHeaderAttribute = typeof(FromHeaderAttribute);
    internal static readonly Type HideFromDocsAttribute = typeof(HideFromDocsAttribute);
    internal static readonly Type Http = typeof(Http);
    internal static readonly Type ICommand = typeof(ICommand);
    internal static readonly Type ICommandOf1 = typeof(ICommand<>);
    internal static readonly Type ICommandHandler = typeof(ICommandHandler);
    internal static readonly Type ICommandHandlerOf1 = typeof(ICommandHandler<>);
    internal static readonly Type ICommandHandlerOf2 = typeof(ICommandHandler<,>);
    internal static readonly Type IDictionary = typeof(IDictionary);
    internal static readonly Type IEndpoint = typeof(IEndpoint);
    internal static readonly Type IEndpointFeature = typeof(IEndpointFeature);
    internal static readonly Type IEndpointValidator = typeof(IEndpointValidator);
    internal static readonly Type IEnumerable = typeof(IEnumerable);
    internal static readonly Type IEnumerableOfIFormFile = typeof(IEnumerable<IFormFile>);
    internal static readonly Type IEventHandler = typeof(IEventHandler);
    internal static readonly Type IEventHandlerOf1 = typeof(IEventHandler<>);
    internal static readonly Type IFormFile = typeof(IFormFile);
    internal static readonly Type IHasMapper = typeof(IHasMapper);
    internal static readonly Type IMapper = typeof(IMapper);
    internal static readonly Type IPlainTextRequest = typeof(IPlainTextRequest);
    internal static readonly Type IResult = typeof(IResult);
    internal static readonly Type ISummary = typeof(ISummary);
    internal static readonly Type IValidator = typeof(IValidator);
    internal static readonly Type JsonIgnoreAttribute = typeof(JsonIgnoreAttribute);
    internal static readonly Type JobQueueOf3 = typeof(JobQueue<,,>);
    internal static readonly Type JobQueueOf4 = typeof(JobQueue<,,,>);
    internal static readonly Type NotImplementedAttribute = typeof(NotImplementedAttribute);
    internal static readonly Type Null = typeof(Null);
    internal static readonly Type Object = typeof(object);
    internal static readonly Type ParseResult = typeof(ParseResult);
    internal static readonly Type QueryParamAttribute = typeof(QueryParamAttribute);
    internal static readonly Type String = typeof(string);
    internal static readonly Type StringSegment = typeof(StringSegment);
    internal static readonly Type SummaryOf1 = typeof(Summary<>);
    internal static readonly Type SummaryOf2 = typeof(Summary<,>);
    internal static readonly Type ToHeaderAttribute = typeof(ToHeaderAttribute);
    internal static readonly Type Uri = typeof(Uri);
    internal static readonly Type ValidatorOf1 = typeof(AbstractValidator<>);
}

readonly struct Null { };