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

| File                       | What it pins down                                                                                                                                                                                                                                                         |
|----------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `HttpParsingTests`         | Parsing the request head: query values with `=`, a head arriving one byte at a time, rejecting conflicting `Content-Length`, `Content-Length` with `Transfer-Encoding`, a second `Host`, a bare LF and a body over the limit; response `Content-Length` measured in bytes |
| `RequestReadingTests`      | A real connection: pipelined requests answered in order, an unread body drained before the next request, a chunked body, `413` for a chunked body over the limit, `408` for an unfinished head                                                                            |
| `RequestValuesTests`       | `GetFromQuery<T>` returns `null` for a missing value and throws a `400` exception for an unparsable one; `ReadJsonAsync<T>` ignores property name case and throws a `400` exception for a body that does not fit; the body is read once unless buffered                   |
| `RoutingTests`             | Matching by path and method (literal vs parameter, `404` vs `405`, `HEAD` for a `GET` endpoint), `400` for a non-numeric route value, filter order from the outer group to the endpoint, `400` for a malformed JSON body                                                  |
| `DependencyInjectionTests` | Lifetimes, `Validate()` catching a singleton that reaches a scoped service through a transient, disposal order                                                                                                                                                            |
| `OverloadTests`            | A real server answers `503` once request slots and the queue are full; an idle keep-alive connection holds no slot                                                                                                                                                                                                   |

---

## Status

All 37 tests pass.

---

## Adding a test

Routes are declared explicitly, so each test builds only the routes it needs. `TestServer`
starts a real server on port `0` with the routes you pass it and, optionally, a builder
setup for settings and services. `SendAsync` sends one request per connection, `SendRawAsync`
sends raw bytes for protocol tests. Write handlers as lambdas next to the test that uses them.

Tests reach `internal` members through `InternalsVisibleTo` declared in `TinyNet.csproj`.
A test that needs a running server asks for port `0` and reads the real one from
`WebApplication.Port`.