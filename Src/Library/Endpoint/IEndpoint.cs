﻿using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using System.Collections.Concurrent;

// ReSharper disable UnusedMemberInSuper.Global

namespace FastEndpoints;

/// <summary>
/// the common interface implemented by all endpoints
/// </summary>
public interface IEndpoint
{
    /// <summary>
    /// the http context of the current request
    /// </summary>
    HttpContext HttpContext { get; } //this is for allowing consumers to write extension methods

    /// <summary>
    /// validation failures collection for the endpoint
    /// </summary>
    List<ValidationFailure> ValidationFailures { get; } //also for extensibility

    /// <summary>
    /// gets the endpoint definition which contains all the configuration info for the endpoint
    /// </summary>
    EndpointDefinition Definition { get; } //also for extensibility

    //key: the type of the endpoint
    static ConcurrentDictionary<Type, string> TestUrlCache { get; } = new();

    internal static void SetTestUrl(Type endpointType, string url)
        => TestUrlCache[endpointType] = url;

    //don't change to internal. this is unofficially exposed to public.
    public static string TestURLFor<TEndpoint>()
        => TestUrlCache[typeof(TEndpoint)];
}

/// <summary>
/// marker interface for endpoint base classes without a request dto
/// </summary>
public interface INoRequest;