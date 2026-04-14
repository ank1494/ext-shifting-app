# Ubiquitous Language — ext-shifting-app

Canonical terminology for the ext-shifting-app codebase: backend, frontend, API, and infrastructure.

**Domain mathematics terms** (simplicial complex, exterior shifting, triangulation, surface, vertex split, critical region, etc.) are defined in [`m2/ext-shifting/UBIQUITOUS_LANGUAGE.md`](m2/ext-shifting/UBIQUITOUS_LANGUAGE.md). This document does not redefine them; it imports and extends them with app-layer concepts.

---

## Terms

### Infrastructure & Runtime

| Term | Definition | Aliases to avoid |
|------|------------|-----------------|
| **ext-shifting-app** | The Docker-based local web application that wraps the ext-shifting M2 codebase and exposes it via a browser UI. A separate repository from ext-shifting. | "the app" (acceptable shorthand in context) |
| **Docker container** | The Linux runtime environment in which both the ASP.NET Core backend and Macaulay2 run. Resolves the platform dependency on Unix for M2. | "the box", "the environment" |
| **Docker volume** | The bind-mounted directory on the host that persists analysis output files across container restarts and browser sessions. All M2 output is written here. | "output directory", "host folder" |
| **git submodule** | The mechanism by which the ext-shifting M2 repo is bundled inside ext-shifting-app. Located at `m2/ext-shifting`. Default bundled case for non-developer users. | "embedded repo", "bundled code" |
| **local mount** | Developer override: replacing the git submodule with a volume-mounted local clone of ext-shifting in `docker-compose.yml`. Allows editing M2 files without rebuilding the Docker image. | "volume override", "dev mount" |

---

### Backend Modules

| Term | Definition | Aliases to avoid |
|------|------------|-----------------|
| **M2 Process Manager** | The central backend abstraction (implemented as `M2ProcessRunner`) responsible for spawning Macaulay2 processes, streaming stdout/stderr in real time, and detecting normal completion vs. error or crash. All M2-invoking features depend on this module. | "M2 runner", "process manager" (acceptable in context) |
| **One-shot mode** | M2 Process Manager operating mode: spawns a Macaulay2 process for a single script file, captures streaming output, and exits. Used by one-off exterior shifting and the file runner. | "script mode", "batch mode" |
| **Interactive mode** | M2 Process Manager operating mode: maintains a persistent Macaulay2 REPL process, accepts input lines, and streams output back in real time. Used by the Developer tab REPL. | "session mode", "REPL mode" |
| **Analysis Job Manager** | The backend component (implemented as `AnalysisJobManager`) that manages the full lifecycle of an iterative analysis run as a state machine. Tracks job state, current iteration, file paths, and accumulated results. Persists state to the Docker volume. Only one analysis job runs at a time. | "job manager", "analysis manager" |
| **Job state** | The current phase of an analysis job. Valid states: `Idle`, `Running`, `Complete`, `Failed`, `Stopped`. Serialized as strings in the API (never as integers). | "status" (use "job state" in prose; `status` in API field names is acceptable) |
| **File System Service** | The backend component that abstracts all interaction with the Docker volume and M2 repo files. Responsible for reading/writing analysis input and output files, listing available `.m2` files, and managing the output directory structure. | "file service", "file manager" |

---

### API

| Term | Definition | Aliases to avoid |
|------|------------|-----------------|
| **`/shift` endpoint** | `POST /shift` — accepts a simplex list and ordering (lex or rev-lex), invokes `extShiftLex` or `extShiftRevLex` via the M2 Process Manager, and returns the shifted complex. Entry point for one-off exterior shifting. | — |
| **`/analysis/start` endpoint** | `POST /analysis/start` — accepts a run name and surface type (or custom file path), starts the iterative analysis job. Returns HTTP 409 if a job is already running. | — |
| **`/analysis/stop` endpoint** | `POST /analysis/stop` — cancels the currently running analysis job and resets job state to `Idle`. | — |
| **`/analysis/status` endpoint** | `GET /analysis/status` — returns the current job state, current iteration number, and any error information. The `status` field is always a string, never an integer. The `currentIteration` field reflects the 0-based iteration counter written by M2 to `ITERATION_COUNTER` on disk. | — |
| **`/analysis/stream` endpoint** | `GET /analysis/stream` — SSE endpoint that streams live M2 stdout lines for the current analysis iteration. Closes the stream when the job finishes or fails. | "log endpoint", "stream endpoint" |
| **`/analysis/results/{runName}/csv` endpoint** | `GET /analysis/results/{runName}/csv` — returns a CSV file of the completed analysis results for the named run. Returns HTTP 404 if the run name is unknown. | "CSV endpoint" |
| **`/files` endpoint** | `GET /files` — returns a sorted JSON array of relative `.m2` file paths available inside the container. | — |
| **`/files/run` endpoint** | `POST /files/run` — runs a selected `.m2` file in one-shot mode and streams output via SSE. Rejects path traversal attempts with HTTP 400. | — |
| **WebSocket endpoint** | The WebSocket connection that bridges the browser terminal (xterm.js) to the interactive M2 REPL process inside the container. | "REPL socket", "terminal endpoint" |
| **SSE** | Server-Sent Events — the HTTP streaming protocol used to push M2 log output from the backend to the browser in real time. Used by both the file runner and iterative analysis. | "event stream", "log stream" (acceptable in UI labels) |
| **Run name** | A user-supplied string identifier for an analysis job, used to name output files and directories on the Docker volume. Provided at job start. | "analysis name", "job name", `analysisName` (the M2-level variable) |
| **Surface type** | The input selector for iterative analysis: one of `Torus`, `KleinBottle`, `ProjectivePlane`, or `Custom`. Determines which bundled irreducible triangulations are used as the starting input, or defers to a custom file path. | "surface selector", "surface dropdown" |
| **Custom file path** | The user-supplied `.m2` file path used as analysis input when surface type is `Custom`. Must resolve to a path inside the container. | "custom input", "custom path" |

---

### Frontend

| Term | Definition | Aliases to avoid |
|------|------------|-----------------|
| **Research tab** | The primary UI tab for performing calculations: one-off exterior shifting and iterative analysis. Intended for mathematics researchers. | "main tab", "compute tab" |
| **Developer tab** | The secondary UI tab for developing and testing the M2 codebase: the interactive REPL and file runner. Intended for developers working on the M2 scripts. | "dev tab", "console tab" |
| **One-off exterior shifting** | The Research tab workflow: paste simplices, choose ordering, click Compute, view the shifted complex. A single M2 invocation in one-shot mode — no iterations, no job state. | "one-off shifting", "single shift" |
| **Iterative analysis** | The Research tab workflow: select surface type and run name, start a job, watch live output across multiple iterations until convergence. Corresponds to driving the `analyze triangs.m2` analysis loop. | "iterative shifting", "analysis run" (avoid — overlaps with the M2-level term; use "analysis job" or "iterative analysis" in the app layer) |
| **Live log** | The output area in the Research tab that displays M2 stdout lines in real time, consumed from the SSE stream. Cleared and hidden when a job stops. | "output box", "log area", "log stream" |
| **REPL** | The browser-based interactive Macaulay2 terminal on the Developer tab, implemented with xterm.js, connected via WebSocket to the persistent M2 process. | "console", "terminal" (acceptable in UI labels) |
| **File runner** | The Developer tab section that lets a developer select any `.m2` file from the mounted repo and run it in one-shot mode, with live output streaming. | "file picker", "script runner" |
| **Run Analysis button** | The Research tab button that starts an iterative analysis job. Hidden while a job is running; replaced by the Stop button. Must never be displayed simultaneously with the Stop button. | — |
| **Stop button** | The Research tab button that cancels a running analysis job. Visible only while a job is running; replaced by the Run Analysis button after stopping. | — |
| **Download CSV button** | Appears in the Research tab after an analysis job reaches convergence. Triggers a download of the results CSV from `/analysis/results/{runName}/csv`. | — |
| **Convergence message** | The UI notification displayed when an iterative analysis completes naturally — i.e., when M2 outputs `no more splits` and no further iteration is scheduled. | "completion message", "done message" |
| **Log replay** | On reconnect or page reload while a job is running, previously emitted log lines are replayed to the browser so the log area is not blank. | "history replay", "output replay" |

---

### M2 Queue (App-Layer References)

| Term | Definition | Aliases to avoid |
|------|------------|-----------------|
| **Analysis queue** | The persistent file-based work queue managed by `queueOps.m2`. Lives under the run output directory as two subdirectories: `pending/` (items awaiting processing) and `done/` (items already processed). The queue is initialised by `initQueue` and driven by `runQueue`. | "work queue", "job queue" |
| **Queue item** | A single unit of work in the analysis queue: one triangulation plus provenance metadata (`parent`, `depth`, `seq`). Serialised as a readable M2 `HashTable` in a zero-padded file (e.g. `0001`). | "work item", "item file" |
| **Pending file** | A queue item file in `pending/` awaiting processing. Written by `writeQueueItem`. Moved to `done/` (as a done file) by `processQueueItem` after the item is processed. | "input file" (within queue context) |
| **Done file** (updated) | A queue item file in `done/` that has been processed. Written by `writeDoneItem`. Contains the same fields as the pending file (`parent`, `depth`, `seq`, `triangulation`) plus a `critRegions` key holding the list of critical region `HashTable` objects found during processing (serialised via `toExternalString`). | "output file" (within queue context) |
| **`item_started` event** (new) | A structured SSE event emitted to stderr by `emitItemStarted` when `processQueueItem` begins processing an item. JSON shape: `{"type":"item_started","item":"<name>","depth":<n>,"parent":"<name>"}`. Forwarded to the browser by the C# SSE pass-through. | — |
| **`item_done` event** (updated) | A structured SSE event emitted to stderr by `emitItemDone` when `processQueueItem` finishes an item. JSON shape: `{"type":"item_done","item":"<name>","splits":<n>,"critRegions":[...]}`. The `critRegions` array contains one object per critical region found, each with `regionShape` (string), `boundaryVertexCount` (integer), and `innerVertexCount` (integer). Previously the event did not include `critRegions`. Forwarded to the browser by the C# SSE pass-through without modification. | — |
| **`run_complete` event** | A structured SSE event emitted when `runQueue` empties `pending/` naturally. JSON: `{"type":"run_complete"}`. | — |
| **`run_paused` event** | A structured SSE event emitted when `runQueue` stops early due to a cap (item cap, vertex cap, timeout, or stop signal). JSON: `{"type":"run_paused"}`. | — |

---

### M2 Scripts & Files (App-Layer References)

| Term | Definition | Aliases to avoid |
|------|------------|-----------------|
| **`analyze triangs.m2`** | The M2 entry point script for one iteration of the iterative analysis. Driven by the Analysis Job Manager. | "the analysis script" |
| **`input_N`** | The input file for iteration N (0-based), containing the list of triangulations to process in that iteration. Written by M2 as output of iteration N−1; read by M2 at the start of iteration N. | "iteration input", "input file" |
| **`ITERATION_COUNTER`** | A file written by M2 to disk after each iteration, containing the 0-based iteration number. The backend reads this file to populate the `currentIteration` field in the status response. The authoritative source — the backend does not maintain its own counter. | "iteration file", "counter file" |
| **`Analysis Summary.txt`** | The per-iteration structured output file written by M2, containing per-triangulation outcomes. Read by the backend to generate the CSV export. | "summary file" |
| **`Analysis Log.txt`** | The per-iteration log file written by M2. Not currently surfaced in the UI (noted as a backlog observability gap). | "log file" |
| **`Exceptions Log.txt`** | The per-iteration exceptions file written by M2. Not currently surfaced in the UI (noted as a backlog observability gap). | "exception file" |

---

## Relationships

- The **M2 Process Manager** is the deep module at the centre of the backend. All three user-facing features (one-off exterior shifting, file runner, iterative analysis) invoke M2 through it. It operates in **one-shot mode** for single-execution tasks and **interactive mode** for the REPL.
- The **Analysis Job Manager** owns the **iterative analysis** lifecycle. It calls the M2 Process Manager for each **iteration**, detects convergence via `no more splits`, and reads the `ITERATION_COUNTER` file to populate the `currentIteration` field in the status response.
- The **File System Service** is used by the Analysis Job Manager and file runner to locate `.m2` files, read and write `input_N` files, and read `Analysis Summary.txt` for CSV generation.
- The **Research tab** exposes two workflows: **one-off exterior shifting** (single M2 call, no job state) and **iterative analysis** (multi-iteration, managed by the Analysis Job Manager). These are distinct; neither is a subset of the other.
- The **Developer tab** exposes two tools: the **REPL** (interactive mode, WebSocket) and the **file runner** (one-shot mode, SSE). Both use the M2 Process Manager.
- The **SSE stream** (`/analysis/stream`) is tied to the current **analysis job**. It closes when the job finishes or fails — not only when convergence occurs. The frontend's `onerror`/`onclose` handler is the trigger for calling `/analysis/status` and updating the UI.
- A **run name** is supplied at job start and used as the key for all output files on the **Docker volume** and as the path parameter in `/analysis/results/{runName}/csv`.
- **Surface type** determines the starting `input_0` file: `Torus` → `irred tori.m2`, `KleinBottle` → `irred kb.m2`, `ProjectivePlane` → `irred pp.m2`, `Custom` → a user-supplied path.
- The **git submodule** at `m2/ext-shifting` is the default source of all M2 scripts. A **local mount** in `docker-compose.yml` overrides it for developer workflows. The app does not distinguish between these two cases at runtime.
- The **analysis queue** is driven by `runQueue`, which calls `processQueueItem` for each item. `processQueueItem` emits an **`item_started` event** at the start and an **`item_done` event** at the end. The C# SSE pass-through forwards these events to the browser without parsing them. The **done file** written per item now includes `critRegions` data co-located with provenance metadata.
- **`item_done` events** carry `critRegions` as a JSON array of objects whose shape mirrors the M2 `HashTable` fields (`regionShape`, `boundaryVertexCount`, `innerVertexCount`) defined in `m2/ext-shifting/UBIQUITOUS_LANGUAGE.md`.

---

## Example Dialogue

> "The **Research tab** shows an **iterative analysis** for the Torus surface type. After three **iterations**, the **convergence message** appeared and the **Download CSV button** became visible. The `/analysis/status` endpoint showed `\"status\": \"Complete\"` and `\"currentIteration\": 2`."

> "The **M2 Process Manager** is in **one-shot mode** for the `/shift` endpoint — it spawns M2, runs `extShiftLex`, captures the output, and exits. The **REPL** uses **interactive mode**: a persistent process kept alive across browser sessions."

> "The **Analysis Job Manager** reads the `ITERATION_COUNTER` file written by M2, not its own loop counter, to populate `currentIteration` in the status response. This keeps the app's reported iteration number consistent with what M2 logs."

> "When a job fails, the **SSE stream** closes immediately. The frontend's `onerror` handler fires, calls `GET /analysis/status`, and updates the **live log** area to show a failure indicator."

---

## Flagged Ambiguities

| Term | Ambiguity | Recommended resolution |
|------|-----------|----------------------|
| `analysis run` | In the M2-level ubiquitous language (`m2/ext-shifting/UBIQUITOUS_LANGUAGE.md`), an "analysis run" is the named batch computation. In the app layer, users initiate an **iterative analysis** job. The app-layer concept is a superset: it adds run name, surface type, job state, SSE streaming, and CSV export. | Use **iterative analysis** for the app-layer workflow. Use **analysis run** only when referring to the M2-level concept. Reserve "analysis job" for referring to the running process managed by the Analysis Job Manager. |
| `status` | Used both as the HTTP response field name (`"status": "Running"`) and colloquially to mean job state. | Use **job state** in prose and documentation. `status` is acceptable as a JSON field name and in API references. |
| `iteration` | An **analysis iteration** is one execution of `analyze triangs.m2` (app layer). As noted in `m2/ext-shifting/UBIQUITOUS_LANGUAGE.md`, "iteration" can also mean one step within the shifting algorithm itself. | Use **analysis iteration** for the app-layer concept (one call to `analyze triangs.m2`). Inherit the ext-shifting glossary distinction for intra-M2 use. |
| `currentIteration` | The API field `currentIteration` is 0-based (derived from `ITERATION_COUNTER`), while the UI may display it as a 1-based "Iteration N of M" label. | The API value is 0-based and authoritative. The UI is free to display 1-based, but must label it clearly (e.g. "Iteration 1" = `currentIteration: 0`). Do not change the API field. |
| `stop` vs `reset` | Clicking Stop cancels the job and resets the job state to `Idle`. There is no separate "reset" operation. | Use **Stop** for the user action. The resulting state is `Idle`. Avoid "reset" as a standalone term. |
| `stream` | Can mean the SSE stream (`/analysis/stream` endpoint), the live log display in the UI, or the general concept of streaming output from M2. | Use **SSE stream** for the endpoint, **live log** for the UI element, and **streaming output** for the general concept. |
| `M2 Process Manager` vs `M2ProcessRunner` | The concept is "M2 Process Manager"; the C# implementation class is `M2ProcessRunner`. | Use **M2 Process Manager** in architecture and documentation. Use `M2ProcessRunner` only when referring to the C# class directly. |
