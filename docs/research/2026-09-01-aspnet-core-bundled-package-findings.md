# Research findings: ASP.NET Core bundled package question

**Date:** 2026-09-01
**Question:** Are the packages used by this repository bundled into ASP.NET Core, so
their `PackageReference` entries can be deleted?

## Scope

The repository has three package references relevant to this question:

- `Microsoft.AspNetCore.OpenApi` 10.0.10 and `Microsoft.OpenApi` 2.7.5 in
  `src/hr-sat.Web.Api/hr-sat.Web.Api.csproj`.
- `MimeKit` 4.17.0 in `src/hr-sat.Application/hr-sat.Application.csproj`.

The phrase "bundled into ASP.NET Core" is interpreted as "provided by the
`Microsoft.AspNetCore.App` shared framework referenced by the Web SDK", rather than
merely available somewhere in the local NuGet cache.

## Findings

### `Microsoft.AspNetCore.OpenApi`

**It is not part of the ASP.NET Core shared framework.** Microsoft’s ASP.NET Core
OpenAPI documentation instructs applications to install the
`Microsoft.AspNetCore.OpenApi` package for built-in OpenAPI document generation. The
package page also identifies it as a NuGet package and lists `Microsoft.OpenApi` as a
dependency.

In this repository, `Program.cs` calls `builder.Services.AddOpenApi()` and
`app.MapOpenApi()`. Removing the package reference while leaving those calls in place
will therefore fail the build because those package-provided APIs are no longer
available. Removing the calls as well would make the development OpenAPI endpoint
disappear; ASP.NET Core does not provide an equivalent OpenAPI generator merely through
the shared framework.

Sources:

- [ASP.NET Core OpenAPI documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi): describes installing `Microsoft.AspNetCore.OpenApi` and using `AddOpenApi`/`MapOpenApi`.
- [Microsoft.AspNetCore.OpenApi on NuGet](https://www.nuget.org/packages/Microsoft.AspNetCore.OpenApi/10.0.10): identifies the package, its .NET 10 target, and its `Microsoft.OpenApi` dependency.
- [ASP.NET Core shared frameworks](https://learn.microsoft.com/en-us/dotnet/core/deploying/framework-dependent-apps): documents `Microsoft.AspNetCore.App` as the shared framework supplied by the ASP.NET Core runtime; it does not turn arbitrary NuGet packages into framework references.

### `Microsoft.OpenApi`

**It is not part of the ASP.NET Core shared framework.** It is the OpenAPI.NET object
model and serialization library distributed as its own NuGet package. When
`Microsoft.AspNetCore.OpenApi` remains referenced, NuGet resolves `Microsoft.OpenApi`
transitively, so the explicit `Microsoft.OpenApi` reference in this repository is
redundant for restore purposes.

Deleting only the explicit `Microsoft.OpenApi` reference should not break this project:
`Microsoft.AspNetCore.OpenApi` still brings it in transitively. Deleting both packages
while retaining `AddOpenApi`/`MapOpenApi` will fail the build. There is no separate
OpenAPI object model supplied by `Microsoft.AspNetCore.App`.

Sources:

- [Microsoft.OpenApi on NuGet](https://www.nuget.org/packages/Microsoft.OpenApi/2.7.5): describes the OpenAPI.NET SDK and its package distribution.
- [Microsoft.AspNetCore.OpenApi 10.0.10 on NuGet](https://www.nuget.org/packages/Microsoft.AspNetCore.OpenApi/10.0.10): records `Microsoft.OpenApi` as a dependency.
- [OpenAPI.NET source repository](https://github.com/microsoft/OpenAPI.NET): first-party source for the Microsoft OpenAPI.NET library.

### `MimeKit`

**It is not bundled into ASP.NET Core or .NET.** MimeKit is a separately distributed
library for creating and parsing MIME messages. The repository’s candidate import path
uses it in `EmlParser.cs` to load `.eml` messages and inspect their attachments.

Deleting the `MimeKit` reference without changing that parser will fail compilation at
the `MimeKit` types. Replacing it with `System.Net.Mail` or `System.Net.Mime` is not a
drop-in solution: the .NET APIs document mail construction/sending and MIME header
types, but do not provide a general parser for an existing `.eml` MIME message with
multipart attachments. The import feature therefore needs MimeKit or another dedicated
MIME parser.

Sources:

- [MimeKit on NuGet](https://www.nuget.org/packages/MimeKit/4.17.0): describes MimeKit as the separately distributed library for MIME message creation and parsing.
- [System.Net.Mail namespace](https://learn.microsoft.com/en-us/dotnet/api/system.net.mail): documents the .NET mail-message and SMTP APIs.
- [System.Net.Mime namespace](https://learn.microsoft.com/en-us/dotnet/api/system.net.mime): documents MIME content-type/content-disposition support, not an `.eml` document parser.
- [MimeKit source repository](https://github.com/jstedfast/MimeKit): first-party source for the library and its MIME parser implementation.

## Recommendation for this repository

- Keep `Microsoft.AspNetCore.OpenApi`; it is required by the current OpenAPI setup.
- The explicit `Microsoft.OpenApi` reference can be removed as a cleanup because it is
  already transitive through `Microsoft.AspNetCore.OpenApi`; verify with a restore/build
  after removal if that cleanup is desired.
- Keep `MimeKit` unless the `.eml` import feature is removed or replaced with another
  dedicated MIME parser.

In short: the ASP.NET Core shared framework does not make any of these three packages
available simply because the application targets ASP.NET Core. Only the explicit
`Microsoft.OpenApi` reference is safely redundant, and that is because another package
supplies it transitively, not because ASP.NET Core bundles it.

## Source list

1. [ASP.NET Core OpenAPI documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi)
2. [ASP.NET Core shared frameworks](https://learn.microsoft.com/en-us/dotnet/core/deploying/framework-dependent-apps)
3. [`Microsoft.AspNetCore.OpenApi` 10.0.10 package](https://www.nuget.org/packages/Microsoft.AspNetCore.OpenApi/10.0.10)
4. [`Microsoft.OpenApi` 2.7.5 package](https://www.nuget.org/packages/Microsoft.OpenApi/2.7.5)
5. [OpenAPI.NET source repository](https://github.com/microsoft/OpenAPI.NET)
6. [`MimeKit` 4.17.0 package](https://www.nuget.org/packages/MimeKit/4.17.0)
7. [System.Net.Mail API reference](https://learn.microsoft.com/en-us/dotnet/api/system.net.mail)
8. [System.Net.Mime API reference](https://learn.microsoft.com/en-us/dotnet/api/system.net.mime)
9. [MimeKit source repository](https://github.com/jstedfast/MimeKit)