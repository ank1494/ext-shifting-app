# ext-shifting-app — Claude Instructions

## README

Audience: non-developer mathematicians who want to run exterior shifting calculations locally without installing Macaulay2 or using a terminal.

Sections to maintain:
- Prerequisites (Docker Desktop only)
- Setup (clone + `docker compose up`)
- Analysis output (where results go)
- Developer: using a local M2 clone (how to mount a local ext-shifting repo)
- Running tests
- API reference (curl examples for each endpoint)

Do NOT add: architecture explanations, C# internals, test philosophy, module design.

## TDD and M2 Testing

When using the `/tdd` skill (or doing any test-driven work) and the changes involve M2 code (files under `m2/ext-shifting/`):

- M2 tests are integrated into the dotnet test suite as integration tests and run via `dotnet test`.
- Do NOT run the full suite on every red-green cycle — M2 integration tests are slow. Run targeted tests during the cycle using filters, e.g.:
  ```
  dotnet test --filter "FullyQualifiedName~SomeSpecificTest"
  ```
- Run the full suite (`dotnet test`) only at the end of a TDD cycle to confirm nothing is broken.
- Do NOT consider the TDD cycle done until the full suite passes.

## Ubiquitous Language

The canonical glossary lives in `UBIQUITOUS_LANGUAGE.md` (app-layer terms) and `m2/ext-shifting/UBIQUITOUS_LANGUAGE.md` (domain mathematics terms). When offering to update the glossary, default to the app-layer file unless the term is purely mathematical.
