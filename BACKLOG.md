# Backlog

## Large / Medium

- **Iterative analysis panel lacks observability** `[medium] [high]`: The iterative analysis panel provides limited visibility into M2's internal state during a run, making it difficult to diagnose issues or understand computation progress without external tooling. The stream only forwards M2's stdout (`print` statements); stderr, and the `Analysis Log.txt` and `Exceptions Log.txt` files M2 writes to disk, are not surfaced. Spans backend and frontend but no architectural overhaul — the data already exists and needs to be exposed.
- **M2 code change behavior** `[large] [mid]`: The app references Macaulay2 code that can change externally (e.g. switching branches on ext-shifting). It is unclear how the app should respond to such changes.

## Small

- **QA: Verify Klein Bottle and Projective Plane surface types** `[small] [mid]`: Section 6.5 of the QA plan (Klein Bottle and Projective Plane iterative analysis) has not yet been tested. Deferred until M2 code is confirmed stable. When ready, run iterative analysis for both surface types and confirm they start and stream output without errors.
- **QA: Verify custom file path surface type** `[small] [mid]`: Section 6.6 of the QA plan (Custom surface type with a user-supplied `.m2` path) has not yet been tested. Deferred until M2 code is confirmed stable. When ready, select Custom, enter a valid `.m2` path inside the container, and confirm the job starts and streams output.
- **QA: Verify CSV export** `[small] [mid]`: Slice 7 (CSV download via UI and API, 404 for unknown run, field correctness) has not yet been tested. Deferred until a full iterative analysis run completes cleanly. When ready, run sections 7.1–7.4 of the QA plan.
- **Paste not working in Developer tab terminal** `[small] [mid]`: Pasting text into the xterm.js REPL terminal does not work. Input must be typed manually.
- **Show absolute path in iterative analysis** `[small] [low]`: When running iterative analysis, the file path shown in the log is relative. It is unclear what the full path resolves to on the host.
- **REPL silent failure on unquoted load** `[small] [low]`: Typing `load filename.m2` without quotes in the Developer tab REPL produces no error and no output, making it appear the file loaded when it did not. Small because this is a UX hint or thin input validation in the frontend with no backend changes needed.
