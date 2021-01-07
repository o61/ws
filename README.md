F# WebSocket Server
===================

MailboxProcessor/Async-based WebSocket server with supervision and server-initiated pings in 200 LOC.

Build
-----

```
$ dotnet build ws.fsproj
```

Run
---

```
$ bin/Debug/net5.0/ws
N2O/F# WebSocket Server 1.0
[smp] [cpu:12] [io:12]
```

Connect
-------

```
$ wscat -c ws://localhost:1900/n2o
```

Benchmark
---------

```
$ tcpkali -c4 --ws --message "PING" \
  --latency-first-byte -r50m 127.0.0.1:1900/n2o \
  --latency-percentiles 50,100
```

MacBook Air 2018

```
Total data sent:     15.6 MiB (16390020 bytes)
Total data received: 8.3 MiB (8721138 bytes)
Bandwidth per channel: 10.038⇅ Mbps (1254.8 kBps)
Aggregate bandwidth: 6.973↓, 13.104↑ Mbps
Packet rate estimate: 70377.7↓, 1141.7↑ (1↓, 17↑ TCP MSS/op)
First byte latency at percentiles: 32.5/46.0 ms (50/100%)
Test duration: 10.006 s.
```

Credits
-------

* Igor Gorodetsky
* Maxim Sokhatsky
