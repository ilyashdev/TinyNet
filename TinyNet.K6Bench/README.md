# Load tests (k6)

Measurements against the simulator endpoints in `LoadControllers.cs`.

[Русская версия](README.ru.md)

---

## Running

Start the server from the `TinyNet.K6Bench` directory — the working directory matters,
since `config.json` and `WebRoot` are resolved relative to it:

```bash
cd TinyNet.K6Bench
dotnet run
```

Run the scenarios from the `loadtests` directory:

```bash
k6 run baseline.js
k6 run knee-io.js
k6 run -e PROFILE=io    io-vs-block.js
k6 run -e PROFILE=block io-vs-block.js
k6 run -e MODE=clean    errors.js
k6 run -e MODE=oversize errors.js
k6 run -e RATE=110000 -e VUS=1024 -e DURATION=10s saturation.js
```

`saturation.js` is the flat-out one: a `constant-arrival-rate` executor with a fixed VU pool,
used to find the throughput ceiling and the point where refusals begin. `RATE`, `VUS`,
`DURATION` and `TARGET` (default `/load/io?ms=0`) are all env knobs; `NOREUSE=1` turns off
connection reuse, which turns the scenario into a fresh-connection storm.

Point them elsewhere with `-e BASE_URL=http://host:port`.

## Changing server settings without editing config.json

`Program.cs` reads environment variables prefixed with `TINYNET_`, where `__` stands
for `:`. Key names are case-sensitive — they are compared as written:

```bash
TINYNET_Server__MaxConcurrentRequests=64 dotnet run    # worker count
TINYNET_Server__MaxQueuedConnections=256 dotnet run    # queue capacity
TINYNET_Server__MaxBodyBytes=1048576 dotnet run        # body limit
```

Source precedence is defaults → `config.json` → environment, so a variable always
wins over the file.

`knee-io.js` needs the same numbers separately, so that it can compute and print its
prediction before the run:

```bash
k6 run -e N=64 -e QUEUE=64 -e MS=1000 knee-io.js
```

## Endpoints

| Path | What is occupied during handling | Purpose |
|---|---|---|
| `/load/cpu?ms=N` | a core; the pool thread does real work | CPU saturation |
| `/load/io?ms=N` | nothing (`Task.Delay`) | the normal shape of a web handler |
| `/load/block?ms=N` | a pool thread, idling (`Thread.Sleep`) | cost of sync code in an async handler |

`ms` is required — without it the binder answers 400.

## What to know before the first run

**Open model, not closed.** Every scenario uses an `*-arrival-rate` executor. A closed
model (`constant-vus`) lowers the offered load by itself once the server degrades, which
hides the queue and the overload — exactly what we want to observe.

**Size the overload and the VU pool from the prediction.** Overload has to be large enough
that the queue actually fills within the stage, and the VU pool has to cover the *worst*
latency, not the unqueued one. `knee-io.js` derives both: overload from "the queue must fill
in 8 s", VUs from the predicted worst latency. When a run reports `dropped_iterations`,
suspect the generator before the server — k6 was not delivering the load it promised.

**Timer resolution on Windows.** `med` and `p(90)` will read as zero on fast responses.
That is not "instant", it is below the measurable resolution. Compare by `p(99)`, `max`
and actual RPS instead.

**Where server time lives.** `http_req_duration` includes connection setup. The server's
own work is `http_req_waiting` (TTFB). With keep-alive the handshake is paid once per
connection instead of once per request, so the two readings are now close.

**Size the worker pool by connections, not by rate.** A worker owns a connection for its
whole life, so `Server:MaxConcurrentRequests` is the number of simultaneous clients. Give the
run fewer VUs than workers, or you are measuring the queue rather than the server: 1024 VUs
against 256 workers answered 200 rps out of 500 offered, at `p(95) = 22.96 s`, with no `503`
and nothing in the server log.

**Ephemeral ports matter only without reuse.** With keep-alive a connection carries up to
`Server:KeepAliveMax` requests, so a normal run burns few ports — 10k requests left 22 sockets
in TIME_WAIT at `KeepAliveMax=1000` against 12625 with keep-alive off. Under `NOREUSE=1`, or
with a low `KeepAliveMax`, the old limit is back: each request burns a port that then sits in
TIME_WAIT for minutes. The default range is 16384 ports:

```bash
netsh int ipv4 show dynamicport tcp                       # the range
powershell "(Get-NetTCPConnection -State TimeWait).Count"  # how many are held
```

Keep such a run under ~15000 requests and pause between runs, or widen the range (requires an
administrator and changes a system setting until reboot):

```
netsh int ipv4 set dynamicport tcp start=10000 num=55000
```

Skip this and you will hit the Windows network stack and mistake it for the server's ceiling.

**Generator and server share one machine.** Acceptable for `/load/io` and `/load/block`.
For `/load/cpu` it is a real distortion: a server that takes every core starves k6. Draw
architectural conclusions from `io` and `block`.

---

## Results

16 logical cores, measured 2026-09-17 and 2026-09-18.

### The ceiling is ~90k rps, and it is not where refusals start

Measured 2026-09-18 on `/load/io?ms=0` (a 23-byte JSON body), keep-alive `1000/5`, N = 2048,
queue = 2048, `ReceiveBufferSize` = 4096.

| Offered | Served | p(95) | `503` | connect-level refusals |
|---|---|---|---|---|
| 60 000 | 59 673 | 2.06 ms | 0 | 298 |
| 90 000 | 84 543 | 11.95 ms | 0 | 794 |
| **110 000** | **91 356** | 13.24 ms | 0 | 841 |
| 130 000 | 80 361 | 38.30 ms | 0 | 3 330 |
| 150 000 | 84 287 | 35.76 ms | 0 | 2 914 |

Throughput saturates near **90k rps**; past that only latency grows. Note the `503` column:
over established connections the server does not refuse anything, it slows down. The
connect-level column is the RST storm when a thousand VUs dial at once against `Listen(1000)`,
not an HTTP answer. k6 shares the machine and stops delivering at the top rows
(`dropped_iterations` reached 655k), so 90k is a floor for the server, not its limit.

Refusals are governed by **connections**, not by rate. Capacity is
`MaxConcurrentRequests + MaxQueuedConnections`, and crossing it produces honest `503`s:

| Capacity | Clients | `200` | `503` |
|---|---|---|---|
| 64 + 64 | 512 | 3 648 | 21 341 |
| 256 + 256 | 1 024 | 16 873 | 33 004 |

Of the tunables only the worker count matters, and only through that rule: at 512 VUs the
settings 512, 1024 and 2048 gave 59 673 / 59 715 / 59 730 rps. `ReceiveBufferSize` of 4096,
8192 and 32768 landed within noise of each other (341 / 365 / 344 µs average).

### Keep-alive settings: the idle timeout is what matters

Same offered load, 256 clients against 64 workers — the regime where connections outnumber
slots:

| `KeepAliveTimeout` | Served | max latency |
|---|---|---|
| 2 s | 908 rps | 8.89 s |
| 5 s | 500 rps | 16.70 s |
| 15 s | 282 rps | 19.92 s + client timeouts |

`KeepAliveMax` does not participate: at a 2 s timeout, 100, 1000 and 10000 all produced the
same 907 rps. It does matter for port churn and for the ceiling — 10k requests left 2588
sockets in TIME_WAIT at `max=4` against 22 at `max=1000`, and a sustained run at `max=100`
burned 8607 ports in ten seconds while dropping the ceiling from 91k to 85k rps.

The reason a short idle timeout wins under overload: a queued connection whose client has
already given up still costs a worker the full timeout once it is finally picked up.

### Capacity follows a formula

Predictions are derived from the code before each run: the throughput ceiling is
`MaxConcurrentRequests`, the wait in a full queue is `MaxQueuedConnections / MaxConcurrentRequests`,
and the worst latency is that wait plus the handler's own time.

`/load/io?ms=1000`, N = 64, queue = 64 — predicted worst latency 2 s:

```
http_req_duration .. med=1.01s  p(95)=1.93s  max=2.02s
status_200 ......... 2395
status_503 .........    6
http_req_failed .... 0.00%
```

Measured `max = 2.02 s` against a predicted 2 s. Capacity can be planned from the formula
rather than guessed, and overload produces honest 503s with bounded latency.

### A blocking handler degrades throughput, not correctness

Same machine, same rate (100 rps), same handler delay (500 ms), N = 256, queue = 128.
Only the way the handler waits differs.

| | `/load/io` (`Task.Delay`) | `/load/block` (`Thread.Sleep`) |
|---|---|---|
| Successful | 3001 | 2316 |
| Honest 503s | 0 | 606 |
| Connection-level failures | 0 | **0** |
| `http_req_failed` | 0.00% | 0.00% |
| Latency | avg 506 ms, max 535 ms | avg 1.50 s, max 3.48 s |
| VUs needed | 51 | up to 228 |

`io` sits exactly on the handler's own 500 ms: 256 workers against an offered 100 rps means
no queue at all.

`block` occupies a thread-pool thread per in-flight request. The pool starts at
`ProcessorCount` and injects new threads slowly, so effective concurrency stays far below N
and the queue fills. Throughput drops and latency rises — that part is unavoidable while
worker tasks run on the shared pool.

What does **not** happen any more is the interesting part. Because `AcceptLoop` runs on its
own thread (`tinynet-accept`) with a blocking `Accept()` and a synchronous `503` write, the
accept path cannot be starved by application code. Every rejected request gets a real HTTP
status that a load balancer, a health check and a metric can all see. Before that change the
same scenario produced 1423 TCP-level refusals, no 503s at all, and an empty server log.
