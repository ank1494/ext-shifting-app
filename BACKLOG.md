# Backlog

- **Split exemption correctness** `[large] [high]`: The split exemption mechanism in `getCritRegions` has gaps that risk incorrect results — both in when exemptions are applied relative to the critical regions search, and in the logic for which splits to exempt based on triangulation symmetry.
  - **Exempt-split filter timing in getCritRegions** `[small] [high]`: The exempt-split filter is applied before the critical regions search, meaning exempted splits are excluded from consideration when identifying critical regions. This ordering risk grows as additional exemption cases are introduced.
  - **Automorphism-aware split filtering** `[large] [mid]`: The analysis does not account for triangulation symmetry when selecting split candidates. Vertices in the same automorphism orbit are redundant to split at, and vertices with non-trivial symmetries may also be skippable — the split exemption mechanism is a likely vehicle for both filters. For each split that is filtered out, it should be documented which non-filtered split in the same orbit covers it. Sized large because automorphism groups must be determined offline per triangulation and encoded as hard-coded exemptions, spanning potentially many triangulations.

- **M2 code change behavior** `[large] [mid]`: The app references Macaulay2 code that can change externally (e.g. switching branches on ext-shifting). It is unclear how the app should respond to such changes.

- **M2 output observability** `[medium] [mid]`: The app and M2 codebase provide limited visibility into computation state, timing, and failure modes during a run.
  - **SSE stream lifecycle events** `[medium] [mid]`: The SSE stream emits no events during queue initialization (directory creation, file seeding), making it impossible to identify a safe stop point. Additionally, M2 scripts emit only `EVENT:`-prefixed lines; plain diagnostic output is silently dropped. Lifecycle events should signal initialization start, seeding complete, and first item pickup. (gh #55)
  - **Output line timestamps (M2 + app layer)** `[medium] [mid]`: M2 output lines carry no timing information, making it impossible to distinguish real-time streaming from a burst of output at process exit. This prevents verification that the buffered-output fix is actually working. Two timestamp sources are needed: M2-side (when the line was emitted) and C# side (when `OutputReceived` fired), surfaced through the SSE stream and displayed in the live log.
  - **Logging gaps in ext-shifting M2 code** `[small] [mid]`: The ext-shifting Macaulay2 codebase has limited logging, making it difficult to trace computation steps or diagnose failures. Gaps in what is printed to stdout reduce the observability surfaced through the app's streaming output.

- **Integration tests (Docker + M2)** `[medium] [mid]`: No test suite exercises the full stack with a real M2 process inside Docker. M2 calculation correctness is covered by the ext-shifting codebase, but the integration seam is untested. (Iterative analysis termination tests + M2 runner infrastructure tracked in #70.)

- **UI: resume controls and batch cap inputs** `[medium] [mid]`: The UI provides no way to resume a paused run or specify batch cap parameters (`itemCap`, `maxVertexCount`, `timeoutSeconds`). The backend endpoints exist; the gap is entirely in the frontend. (gh #54)

- **CSV compilation from host done/ folder** `[medium] [mid]`: The CSV compilation currently works only from within the Docker container. There is no way to compile a CSV from a `done/` folder on the host machine, blocking users who run analyses on WSL. Should be a separate C# executable project that takes a `done/` folder path as input and outputs the CSV; the done-file→CSV logic should be extracted into a shared library so both the web app and the new executable can reuse it.

- **Graphical interface** `[large] [low]`: The app has no graphical representations — input is text-only and output is list-only.
  - **Graphical complex input** `[large] [low]`: No mechanism for users to specify a simplicial complex by drawing it — input is text-only. Identified in the PRD as a valuable future feature; the data model should not preclude this approach.
  - **Output visualisation** `[medium] [low]`: The shifted complex is displayed as a list; there is no graphical rendering. Noted as future work in the PRD.

- **Remove REPL feature** `[medium] [low]`: The Developer tab REPL (xterm.js terminal and backend M2 process connection) is no longer a maintained feature. It should be removed from the UI, backend, and Docker configuration.

- **Macaulay2 package lifecycle** `[small] [low]`:
  - **Docker installPackage migration** `[small] [low]`: The app currently bundles M2 source via the submodule; no discrepancy risk exists with the current approach. Optionally migrate to `installPackage` once a versioned release workflow for ext-shifting exists. Depends on Macaulay2 package publication.
  - **Audit and scope package exports** `[small] [low]`: The initial package exports all `lib/` symbols without distinguishing public API from internal helpers. Internal helpers may need to be relocated and their `TEST ///` blocks converted to standalone scripts. Depends on initial package publication.

- **Show absolute path in iterative analysis** `[small] [low]`: When running iterative analysis, the file path shown in the log is relative. It is unclear what the full path resolves to on the host.

- **Remove batch-file iteration terminology from user-facing outputs** `[small] [low]`: The old batch-file iteration model introduced terms like `currentIteration` that no longer apply to the queue-based analysis. Any remnants of this terminology in the API response body, SSE events, UI labels, README, and ubiquitous language files should be identified and removed to avoid confusion. (Issue #8 was closed as deprecated; this item tracks the cleanup.)
