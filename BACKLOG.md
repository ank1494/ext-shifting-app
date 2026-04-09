# Backlog

- **M2 output observability** `[medium] [mid]`: The app and M2 codebase provide limited visibility into computation state, timing, and failure modes during a run.
  - **Output line timestamps (M2 + app layer)** `[medium] [mid]`: M2 output lines carry no timing information, making it impossible to distinguish real-time streaming from a burst of output at process exit. This prevents verification that the buffered-output fix is actually working. Two timestamp sources are needed: M2-side (when the line was emitted) and C# side (when `OutputReceived` fired), surfaced through the SSE stream and displayed in the live log.
  - **Logging gaps in ext-shifting M2 code** `[small] [mid]`: The ext-shifting Macaulay2 codebase has limited logging, making it difficult to trace computation steps or diagnose failures. Gaps in what is printed to stdout reduce the observability surfaced through the app's streaming output.

- **Test coverage** `[medium] [high]`: The codebase has no automated tests for key logic paths or full-stack behavior. Correctness of iterative analysis steps and the Docker/M2 integration seam are both unverified.
  - **Iterative analysis unit/integration tests** `[medium] [high]`: The iterative analysis loop has no automated tests that exercise intermediate steps. Given a known triangulation (e.g. an eight-vertex triangulation of the torus), a test should select one non-critical split whose edge shifting is not yet a prefix, repeat, and assert termination within a surface-dependent vertex bound N. N must be confirmed per surface before the assertion can be written.
  - **Integration tests (Docker + M2)** `[medium] [mid]`: No test suite exercises the full stack with a real M2 process inside Docker. Explicitly deferred in the PRD; M2 calculation correctness is covered by the ext-shifting codebase, but the integration seam is untested.

- **M2 code change behavior** `[large] [mid]`: The app references Macaulay2 code that can change externally (e.g. switching branches on ext-shifting). It is unclear how the app should respond to such changes.

- **Macaulay2 package lifecycle** `[large] [mid]`: The ext-shifting M2 code is not yet published as a proper Macaulay2 package; the Docker image currently bundles source rather than installing a published package.

- **Automorphism-aware split filtering** `[large] [mid]`: The analysis does not account for triangulation symmetry when selecting split candidates. Vertices in the same automorphism orbit are redundant to split at, and vertices with non-trivial symmetries may also be skippable — the split exemption mechanism is a likely vehicle for both filters. For each split that is filtered out, it should be documented which non-filtered split in the same orbit covers it. Sized large because automorphism groups must be determined offline per triangulation and encoded as hard-coded exemptions, spanning potentially many triangulations.
  - **Macaulay2 package publication** `[large] [mid]`: The ext-shifting M2 code is bundled as a git submodule; it has not been published as a proper Macaulay2 package. Noted as a future goal in the PRD with architectural implications for how the Docker image bundles M2 code.
  - **Docker installPackage migration** `[small] [mid]`: Once the ext-shifting M2 code is published as a Macaulay2 package, the Dockerfile should install it via `installPackage` rather than bundling source. Depends on Macaulay2 package publication.

- **Run name conflicts on restart** `[medium] [mid]`: When starting an analysis with a run name that already has on-disk state from a previous run, the behavior is undefined — output directories may conflict, iteration counters may be stale, and errors may occur mid-run. Spans job lifecycle management on the C# side and may require UI affordances for cleaning up or reusing old runs.

- **QA: Deferred verifications** `[small] [mid]`: Several QA plan sections remain untested, deferred pending M2 code stability and a clean iterative analysis run.
  - **QA: Verify Klein Bottle and Projective Plane surface types** `[small] [mid]`: Section 6.5 of the QA plan (Klein Bottle and Projective Plane iterative analysis) has not yet been tested. Deferred until M2 code is confirmed stable. When ready, run iterative analysis for both surface types and confirm they start and stream output without errors.
  - **QA: Verify custom file path surface type** `[small] [mid]`: Section 6.6 of the QA plan (Custom surface type with a user-supplied `.m2` path) has not yet been tested. Deferred until M2 code is confirmed stable. When ready, select Custom, enter a valid `.m2` path inside the container, and confirm the job starts and streams output.
  - **QA: Verify CSV export** `[small] [mid]`: Slice 7 (CSV download via UI and API, 404 for unknown run, field correctness) has not yet been tested. Deferred until a full iterative analysis run completes cleanly. When ready, run sections 7.1–7.4 of the QA plan.

- **Developer tab REPL UX gaps** `[small] [mid]`: The Developer tab REPL has multiple UX gaps that make interactive use unreliable.
  - **Paste not working in Developer tab terminal** `[small] [mid]`: Pasting text into the xterm.js REPL terminal does not work. Input must be typed manually.
  - **REPL silent failure on unquoted load** `[small] [low]`: Typing `load filename.m2` without quotes in the Developer tab REPL produces no error and no output, making it appear the file loaded when it did not. Small because this is a UX hint or thin input validation in the frontend with no backend changes needed.

- **Graphical interface** `[large] [low]`: The app has no graphical representations — input is text-only and output is list-only.
  - **Graphical complex input** `[large] [low]`: No mechanism for users to specify a simplicial complex by drawing it — input is text-only. Identified in the PRD as a valuable future feature; the data model should not preclude this approach.
  - **Output visualisation** `[medium] [low]`: The shifted complex is displayed as a list; there is no graphical rendering. Noted as future work in the PRD.

- **Show absolute path in iterative analysis** `[small] [low]`: When running iterative analysis, the file path shown in the log is relative. It is unclear what the full path resolves to on the host.
