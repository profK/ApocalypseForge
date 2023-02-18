// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System
open System.Threading.Tasks
open Discord
open Discord.WebSocket

// Define a function to construct a message to print
let from whom =
    sprintf "from %s" whom



let LogFunc = System.Func<LogMessage,Task>(
     fun(message)->
         async {
             Console.WriteLine message.Message
         }
         |> Async.StartAsTask
         :> Task
)
let token = Environment.GetEnvironmentVariable("TOKEN");
[<EntryPoint>]    
let main argv =
    let client = new DiscordSocketClient()
    client.add_Log LogFunc
    
    Async.AwaitTask (client.LoginAsync(TokenType.Bot, token)) |> ignore
    Async.AwaitTask (client.StartAsync()) |> ignore

    // Block this task until the program is closed.
    Async.AwaitTask (Task.Delay(-1)) |> ignore
    
    while true do
        ()
    0 // return an integer exit code