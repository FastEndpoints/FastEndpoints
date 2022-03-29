---
title: Welcome
_disableAffix: true
---

<div class="logo-container">
  <img src="images/logo.svg">
</div>

<span class="center-content">

[![license](https://img.shields.io/github/license/dj-nitehawk/FastEndpoints?color=blue&label=license&logo=Github&style=flat-square)](https://github.com/dj-nitehawk/FastEndpoints/blob/master/README.md) [![nuget](https://img.shields.io/nuget/v/FastEndpoints?label=version&logo=NuGet&style=flat-square)](https://www.nuget.org/packages/FastEndpoints) [![tests](https://img.shields.io/azure-devops/tests/RyanGunner/FastEndpoints/6?color=blue&label=tests&logo=Azure%20DevOps&style=flat-square)](https://dev.azure.com/RyanGunner/FastEndpoints/_build/latest?definitionId=6) [![nuget](https://img.shields.io/nuget/dt/FastEndpoints?color=blue&label=downloads&logo=NuGet&style=flat-square)](https://www.nuget.org/packages/FastEndpoints) [![discord](https://img.shields.io/discord/933662816458645504?color=blue&label=discord&logo=discord&logoColor=white&style=flat-square)](https://discord.gg/yQZ4uvfF2E)

</span>

<div class="centered-div">

A light-weight REST Api framework for ASP.Net 6 that implements **[REPR (Request-Endpoint-Response) Pattern](https://deviq.com/design-patterns/repr-design-pattern)**.

**FastEndpoints** offers a better alternative than the **Minimal Api** and **MVC Controllers** with the aim of increasing developer productivity. Performance is on par with the Minimal Api and is faster; uses less memory; and does around **[45k more requests per second](https://fast-endpoints.com/wiki/Benchmarks.html)** than a MVC Controller in a head-to-head comparison.

<br/>

<span class="center-content">
  <img src="images/code-sample.png">
</span>

# Features
- Define endpoints in multiple class files (even in deeply nested folders)
- Auto discovery & registration of endpoints
- Attribute-free endpoint definitions (no attribute argument type restrictions)
- Secure by default & supports most auth providers
- Built-in support for JWT Bearer auth scheme
- Supports policy/permission/role/claim based security
- Declarative security policy building (in each endpoint)
- Supports any IOC container compatible with asp.net
- Constructor & property injection of endpoint dependencies
- Easy model binding from route/query/json body/claims/forms/headers
- Json source generator support for request/response dtos
- Easy file handling (multipart/form-data)
- Model validation with FluentValidation rules
- Convenient business logic validation & error responses
- Easy access to environment & configuration settings
- API versioning
- Rate limiting
- Response caching
- In-process pub/sub event notifications (with auto discovery)
- Easy Server-Sent-Events real-time data streaming
- Convenient integration testing (route-less & strongly-typed)
- Built-in uncaught exception handler
- Supports swagger/serilog/etc.
- Visual studio extension (vsix) for easy vertical slice feature scaffolding
- Plays well with the asp.net middleware pipeline
- Plus anything else the `Minimal APIs` can do...
</div>

---

<div class="actions-container">
<div><a href="https://dev.to/djnitehawk/building-rest-apis-in-net-6-the-easy-way-3h0d">Tutorial</a></div>
  <div><a href="wiki/Get-Started.md">Documentation</a></div>
  <div><a href="wiki/Benchmarks.md">Benchmarks</a></div>
</div>

---

<div class="actions-container">
  <a href="https://www.paypal.com/donate?hosted_button_id=AU3SCQX9FXYCS">
    <img src="images/donate.png" style="margin-top:20px;"/>
  </a>
</div>
