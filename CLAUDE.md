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

- Always include M2 tests as part of the TDD cycle.
- M2 tests cannot be run automatically — a human must run them in an M2 terminal.
- After writing or modifying M2 test code, tell the user exactly what command to run, e.g.:
  ```
  loadPackage "ext-shifting"; check "ext-shifting"
  ```
  or the specific test file/block to evaluate.
- Wait for the user to report results before marking the M2 test step complete.
- Do NOT consider the TDD cycle done until M2 tests have been confirmed passing by the user.

## Ubiquitous Language

The canonical glossary lives in `UBIQUITOUS_LANGUAGE.md` (app-layer terms) and `m2/ext-shifting/UBIQUITOUS_LANGUAGE.md` (domain mathematics terms). When offering to update the glossary, default to the app-layer file unless the term is purely mathematical.
