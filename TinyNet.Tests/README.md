# TinyNet.Tests

Test suite for the framework, on xUnit.

[Русская версия](README.ru.md)

---

## Running

```bash
dotnet test TinyNet.Tests
```

---

## What is covered

The suite is deliberately small. It covers what breaks silently, what cannot be reproduced
by hand, and what has broken before — not everything the framework does.

| File | What it pins down |
|---|---|
| `HttpParsingTests` | Parsing the request head; `Content-Length` measured in bytes |
| `ParameterBindingTests` | Binding `[FromQuery]` and `[FromBody]` |
| `RequestReadingTests` | Reading a request split across TCP reads, down to one byte per read |
| `StaticFileTests` | Serving a file and rejecting paths that lead outside the web root |
| `DependencyInjectionTests` | Lifetimes, and what a singleton holds when it depends on a scoped service |
| `OverloadTests` | A real server on an OS-chosen port answers `503` once its queue is full |

---

## Status

Sixteen of nineteen tests pass. The three red ones pin down the query and body binding
listed under **In progress** in the [root README](../README.md); they are expected to fail
until that work lands.

---

## Adding a test

`InitControllers` scans every loaded assembly, so the controllers in `TestControllers.cs`
are visible to all tests at once and their `[Route]` values must stay unique. Add new test
controllers to that file rather than next to the test that uses them.

Tests reach `internal` members through `InternalsVisibleTo` declared in `TinyNet.csproj`.
A test that needs a running server asks for port `0` and reads the real one from
`WebApplication.Port`.