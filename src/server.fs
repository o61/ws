namespace N2O

open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Net.WebSockets
open System.Threading

// Pure MailboxProcessor-based WebSocket Server

module Server =

    let mutable interval = 5000
    let mutable ticker = true // enable server-initiated Tick messages

    let startClient (tcp: TcpClient) (sup: MailboxProcessor<Sup>) (ct: CancellationToken) =
        MailboxProcessor.Start(
            (fun (inbox: MailboxProcessor<Msg>) ->
                async {
                    let ns = tcp.GetStream()
                    let size = tcp.ReceiveBufferSize
                    let bytes = Array.create size (byte 0)
                    let! len = ns.ReadAsync(bytes, 0, bytes.Length) |> Async.AwaitTask

                    try
                        let req = Req.parse (getLines bytes len)
                        if isWebSocketsUpgrade req then
                            do! ns.AsyncWrite (handshake req)
                            let ws =
                                WebSocket.CreateFromStream(
                                    (ns :> Stream), true, "n2o", TimeSpan(1, 0, 0))
                            sup.Post(Connect(inbox, ws))
                            if ticker then Async.Start(telemetry ws inbox ct sup, ct)
                            return! looper ws req size ct sup
                        else ()
                    finally tcp.Close ()
                }),
            cancellationToken = ct
        )

    let heartbeat (interval: int) (ct: CancellationToken) (sup: MailboxProcessor<Sup>) =
        async {
            while not ct.IsCancellationRequested do
                do! Async.Sleep interval
                sup.Post(Tick)
        }

    let listen (listener: TcpListener) (ct: CancellationToken) (sup: MailboxProcessor<Sup>) =
        async {
            while not ct.IsCancellationRequested do
                let! client = listener.AcceptTcpClientAsync() |> Async.AwaitTask
                client.NoDelay <- true
                startClient client sup ct |> ignore
        }

    let startSupervisor (ct: CancellationToken) =
        MailboxProcessor.Start(
            (fun (inbox: MailboxProcessor<Sup>) ->
                let listeners = ResizeArray<_>()
                async {
                    while not ct.IsCancellationRequested do
                        match! inbox.Receive() with
                        | Close ws -> ()
                        | Connect (l, ns) -> listeners.Add(l)
                        | Disconnect l -> listeners.Remove(l) |> ignore
                        | Tick -> listeners.ForEach(fun l -> l.Post Nope)
                }),
            cancellationToken = ct
        )

    let start (addr: string) (port: int) =
        let cts = new CancellationTokenSource()
        let token = cts.Token
        let sup = startSupervisor token
        let listener = TcpListener(IPAddress.Parse(addr), port)

        try listener.Start(10) with
        | :? SocketException -> failwithf "%s:%i is acquired" addr port
        | err -> failwithf "%s" err.Message

        Async.Start(listen listener token sup, token)
        if ticker then Async.Start(heartbeat interval token sup, token)

        { new IDisposable with member x.Dispose() = cts.Cancel() }
