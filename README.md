# ext-shifting-app

A local web application for running [ext-shifting](https://github.com/ank1494/ext-shifting) Macaulay2 calculations without needing to install Macaulay2 or use a Unix terminal.

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

That's it.

## Setup

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
