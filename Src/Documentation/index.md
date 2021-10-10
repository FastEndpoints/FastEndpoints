---
title: Welcome
---

![](images/header.svg)

[![license](https://img.shields.io/github/license/dj-nitehawk/FastEndpoints?color=blue&label=license&logo=Github&style=flat-square)](https://github.com/dj-nitehawk/FastEndpoints/blob/master/README.md) [![nuget](https://img.shields.io/nuget/v/FastEndpoints?label=version&logo=NuGet&style=flat-square)](https://www.nuget.org/packages/FastEndpoints) [![nuget](https://img.shields.io/nuget/dt/FastEndpoints?color=blue&label=downloads&logo=NuGet&style=flat-square)](https://www.nuget.org/packages/FastEndpoints)

# Intro
An alternative for building RESTful Web APIs with ASP.Net 6 which encourages CQRS and Vertical Slice Architecture.
**FastEndpoints** offers a more elegant solution than the **Minimal APIs** and **MVC Controllers**.
Performance is on par with the Minimal APIs and is faster; uses less memory; and outperforms a traditional MVC Controller by about **[34k requests per second](wiki/Benchmarks.md)** on a Ryzen 3700X desktop.

# Features
- Define your endpoints in multiple class files (even in deeply nested folders)
- Auto discovery and registration of endpoints
- Attribute-free endpoint definitions (no attribute argument type restrictions)
- Secure by default and supports most authentication/authorization providers
- Built-in support for JWT Bearer auth scheme
- Supports policy/permission/role/claim based security
- Declarative security policy building (inside each endpoint)
- Supports any IOC container (compatible with asp.net)
- Dependencies are automatically property injected
- Model binding support from route/json body/claims/forms
- Model validation using FluentValidation rules
- Convenient business logic validation and error responses
- Easy access to environment and configuration settings
- Supports pipeline behaviors like MediatR
- Supports in-process pub/sub event notifications
- Auto discovery of event notification handlers
- Convenient integration testing (route-less and strongly-typed)
- Plays well with the asp.net middleware pipeline
- Supports swagger/serilog/etc.
- Visual studio extension (vsix) for easy vertical slice feature scaffolding
- Plus anything else the `Minimal APIs` can do...

---

<div class="actions-container">
  <div><a href="wiki/Get-Started.md">Get Started</a></div>
  <div><a href="wiki/Benchmarks.md">See Benchmarks</a></div>
</div>

---

<div class="actions-container">
  <a href="https://www.paypal.com/donate?hosted_button_id=AU3SCQX9FXYCS">
    <img src="images/donate.png" style="margin-top:20px;"/>
  </a>
</div>
