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

## Ubiquitous Language

The canonical glossary lives in `UBIQUITOUS_LANGUAGE.md` (app-layer terms) and `m2/ext-shifting/UBIQUITOUS_LANGUAGE.md` (domain mathematics terms). When offering to update the glossary, default to the app-layer file unless the term is purely mathematical.
