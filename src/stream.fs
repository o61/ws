namespace N2O

open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Net.WebSockets
open System.Text
open System.Threading
open System.Runtime.Serialization
open System.Security.Cryptography

// Async WebSocket Combinators

[<AutoOpen>]
module Stream =

    let mutable protocol : byte[] -> byte[] = fun x -> x

    let send (ns: WebSocket) (ct: CancellationToken) (bytes: byte[]) =
        async {
            ns.SendAsync(ArraySegment<byte>(bytes),
                WebSocketMessageType.Binary, true, ct) |> ignore }

    let runTelemetry (ws: WebSocket)
                     (inbox: MailboxProcessor<Payload>)
                     (ct: CancellationToken)
                     (ctrl: MailboxProcessor<Sup>)
                     =
        async {
            try
                while not ct.IsCancellationRequested do
                    let! _ = inbox.Receive()
                    do! send ws ct ("TICK" |> Encoding.ASCII.GetBytes)
            finally
                printfn "PUSHER DIE"
                ctrl.Post(Disconnect <| inbox)
                ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, "PUSHER DIE", ct) |> ignore
        }

    let runLoop (ws: WebSocket)
                (size: int)
                (inbox: MailboxProcessor<Payload>)
                (ct: CancellationToken)
                (ctrl: MailboxProcessor<Sup>)
                =
        async {
            try
                let mutable bytes = Array.create size (byte 0)
                while not ct.IsCancellationRequested do
                    let! (result: WebSocketReceiveResult) =
                        ws.ReceiveAsync(ArraySegment<byte>(bytes), ct) |> Async.AwaitTask
                    match (result.MessageType) with
                    | WebSocketMessageType.Text   -> do! send ws ct (protocol bytes.[0..result.Count])
                    | WebSocketMessageType.Binary -> do! send ws ct (protocol bytes.[0..result.Count])
                    | WebSocketMessageType.Close  -> ()
                    | _ -> printfn "PROTOCOL VIOLATION"
            finally
                printfn "LOOPER DIE"
                ctrl.Post(Disconnect <| inbox)
                ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, "LOOPER DIE", ct) |> ignore
        }
