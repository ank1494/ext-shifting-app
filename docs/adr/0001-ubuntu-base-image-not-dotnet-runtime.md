# Ubuntu base image for the final stage, not the .NET runtime image

The Docker image is a multi-stage build: the application is built on
`mcr.microsoft.com/dotnet/sdk:8.0`, but the final stage is `ubuntu:22.04` with
Macaulay2 and the ASP.NET Core runtime installed from packages, rather than the
obvious `mcr.microsoft.com/dotnet/aspnet:8.0`. Macaulay2 is a *runtime*
dependency — the M2 Process Manager spawns `M2` processes while the app is
serving requests — and it is only distributed for Ubuntu via the
`ppa:macaulay2/macaulay2` PPA, which cannot be installed onto the Microsoft
runtime image. The multi-stage split still pays for itself: it keeps the .NET
SDK out of the shipped image, which is the bulk of the size saving.

## Consequences

The final image is larger than a typical ASP.NET Core image and will stay that
way — Macaulay2 and its dependencies are irreducible here. Do not "simplify"
this to a single `aspnet`-based stage; the application will build and start, and
then fail at the first analysis run when `M2` is not on the PATH.
