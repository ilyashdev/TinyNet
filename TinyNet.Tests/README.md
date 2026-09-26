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
| `HttpParsingTests` | Parsing the request head; a malformed JSON body with a JSON `Content-Type` is rejected, other bodies are not parsed as JSON; `Content-Length` measured in bytes |
| `RequestValuesTests` | `HttpContext.GetFrom…` returns `null` for a missing or unparsable value; `ReadFromBodyAsync<T>` ignores property name case and returns `null` for a missing or mismatched body |
| `RoutingTests` | Matching by path and method (literal vs parameter, `404` vs `405`), group base paths, filter order, a malformed JSON body answered `400` before the controller |
| `RequestReadingTests` | Reading a request split across TCP reads, down to one byte per read |
| `StaticFileTests` | Serving a file and rejecting paths that lead outside the web root |
| `DependencyInjectionTests` | Lifetimes, and `Validate()` catching a singleton that reaches a scoped service through a transient |
| `OverloadTests` | A real server on an OS-chosen port answers `503` once its queue is full |

---

## Status

All 37 tests pass.

---

## Adding a test

Routes are declared explicitly, so each test builds only the routes it needs. `TestServer`
starts a real server on port `0` with the routes you pass it and sends one request per
connection. Keep test controllers next to the test that uses them.

Tests reach `internal` members through `InternalsVisibleTo` declared in `TinyNet.csproj`.
A test that needs a running server asks for port `0` and reads the real one from
`WebApplication.Port`.