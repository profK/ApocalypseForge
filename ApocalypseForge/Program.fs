// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System
open System.Threading.Tasks
open Discord
open Discord.WebSocket
open Elmish

type Model =
    {
        Value : int
    }
type Msg =
    | Increment
    | Decrement



let LogFunc = System.Func<LogMessage,Task>( fun(message)->
    async {
        Console.WriteLine message.Message
    }
    |> Async.StartAsTask
    :> Task )
let token = Environment.GetEnvironmentVariable("TOKEN")
let client = new DiscordSocketClient()
let connectClient() =
    client.add_Log LogFunc
    
    Async.AwaitTask (client.LoginAsync(TokenType.Bot, token)) |> ignore
    Async.AwaitTask (client.StartAsync()) |> ignore

    // Block this task until the program is closed.
    Async.AwaitTask (Task.Delay(-1)) |> ignore
   
let init () =
    {
        Value = 0
    }
    , []

let update msg model =
    match msg with
    | Increment when model.Value < 2 ->
        { model with
            Value = model.Value + 1
        }
        , []
    | Increment ->
        { model with
            Value = model.Value + 1
        }
        , []
    | Decrement when model.Value > 1 ->
        { model with
            Value = model.Value - 1
        }
        , []
    | Decrement ->
        { model with
            Value = model.Value - 1
        }
        , []
        
let discordStart initial =
    let startfunc dispatch =
        match client.ConnectionState with
        | Disconnected -> connectClient()
        dispatch(Increment)
        { new IDisposable with
                member _.Dispose() = printf "disposed" }
    startfunc   
let subscription model =        
     [ ["increment"], discordStart  Increment]

[<EntryPoint>]    
let main argv =
    Program.mkProgram init update (fun model _ -> printf "%A\n" model)
    |> Program.withSubscription subscription  
    |> Program.run
    while (true) do System.Threading.Thread.Sleep(1000);
    0