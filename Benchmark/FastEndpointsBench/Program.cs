﻿using FastEndpoints;
using FEBench;

var builder = WebApplication.CreateBuilder();
builder.Logging.ClearProviders();
builder.Services.AddFastEndpoints();
builder.Services.AddScoped<ScopedValidator>();

var app = builder.Build();
app.UseFastEndpoints(c => c.Binding.ReflectionCache.AddFromFEBench());
app.Run();

namespace FEBench
{
    public class Program { }
}