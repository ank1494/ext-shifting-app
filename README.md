# ext-shifting-app

A local web application for running [ext-shifting](https://github.com/ank1494/ext-shifting) Macaulay2 calculations without needing to install Macaulay2 or use a Unix terminal.

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

That's it.

## Setup

Make sure Docker Desktop is running before proceeding.

```bash
git clone --recurse-submodules https://github.com/ank1494/ext-shifting-app.git
cd ext-shifting-app
docker compose up
```

Then open [http://localhost:5000](http://localhost:5000) in your browser.

> **First run:** Docker will build the image, which includes installing Macaulay2 and .NET. This takes several minutes. Subsequent starts are fast.

## Analysis output

Results are saved to the `./output` folder next to the repo. You can change this by editing the volume path in `docker-compose.yml`.

## Developer: using a local M2 clone

If you want to edit the Macaulay2 code and have changes reflected immediately (without rebuilding the image), uncomment the developer override volume in `docker-compose.yml`:

```yaml
volumes:
  - ./output:/output
  - ../ext-shifting:/m2/ext-shifting  # <- uncomment this line
```

This replaces the bundled submodule with your local clone of ext-shifting.

## Running tests

```bash
cd src
dotnet test
```

## API reference

All endpoints are on `http://localhost:5000`.

> **Windows CMD users:** The examples below use single quotes, which only work in bash (Git Bash, WSL) and PowerShell. In Command Prompt, replace `'...'` with `"..."` and escape inner quotes with `\"`, e.g. `-d "{\"runName\": \"my-run\"}"`.


### Start an analysis run

```bash
# Built-in surface type (torus | kleinbottle | projectiveplane)
curl -s -X POST http://localhost:5000/analysis/start \
  -H "Content-Type: application/json" \
  -d '{"runName": "my-run", "surfaceType": "torus"}'

# Custom M2 file path
curl -s -X POST http://localhost:5000/analysis/start \
  -H "Content-Type: application/json" \
  -d '{"runName": "my-run", "customFilePath": "/m2/ext-shifting/my-input.m2"}'
```

### Check run status

```bash
curl -s http://localhost:5000/analysis/status
```

Returns JSON with `runName`, `status` (`Idle` | `Running` | `Complete` | `Failed`), `currentIteration`, and `error`.

### Stream live output (Server-Sent Events)

```bash
curl -s -N http://localhost:5000/analysis/stream
```

Replays the existing log then streams new lines until the run finishes or you cancel (`Ctrl+C`).

### Get results for a specific iteration

```bash
curl -s "http://localhost:5000/analysis/results/3?runName=my-run"
```

Omit `runName` to use the currently active run.

### Download all results as CSV

```bash
curl -s http://localhost:5000/analysis/results/my-run/csv -o my-run-results.csv
```

### Stop a running analysis

```bash
curl -s -X POST http://localhost:5000/analysis/stop
```

### Other endpoints

```bash
# List available M2 files
curl -s http://localhost:5000/files

# Run an M2 file (streams output as SSE)
curl -s -N -X POST http://localhost:5000/files/run \
  -H "Content-Type: application/json" \
  -d '{"file": "my-script.m2"}'
```
