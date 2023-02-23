module ApocalypseForge.ApocalypseForge

open System.Threading
open Discord.WebSocket
open System
open System.Text.RegularExpressions
open System.Threading.Tasks
open Discord
open System.Linq
type DicePool =
    {
        d6:int
        plus:int
    }
type RollResult =
    | CriticalFailure
    | Failure 
    | PartialSuccess
    | Success
    | CriticalSuccess
    
type Msg =
    | Connect
    | Connected
    | Disconnected
    | Roll of DicePool
    
    

type Random with
    // Generates an infinite sequence of random numbers within the given range.
    member this.GetValues(minValue, maxValue) =
        Seq.initInfinite (fun _ -> this.Next(minValue, maxValue))
        
let infiniRand = Random()

// Discord interface
let LogFunc (logMessage:LogMessage) : Task= 
    async {
        Console.WriteLine logMessage.Message
    }
    |> Async.StartAsTask
    :> Task 
let token = Environment.GetEnvironmentVariable("TOKEN")

let client = new DiscordSocketClient()
let systemMailbox = MailboxProcessor.Start(fun inbox ->
        let rec messageLoop() = async {

            // read a message
            let! msg = inbox.Receive()

            // process a message
            printfn $"message is: %s{msg}"

            // loop to top
            return! messageLoop()
        }
        messageLoop()
    )
let do_roll pool =
    let numd6 = if pool.d6<2 then 3 else pool.d6
    let rolls =
        infiniRand.GetValues(1,7) |> Seq.take numd6 |> Seq.toArray   
    let kept =
        if pool.d6<2 then
            rolls |> Array.sort |> Array.take 2 
        else
            rolls |> Array.sortDescending |> Array.take 2
    let result =
        kept |> Seq.sum |> fun tot -> tot + pool.plus
        
   (* // return description string    
    $"""
Rolling pool of %i{pool.d6}d6%+i{pool.plus}
Rolls: %A{rolls}
Kept: %A{kept}
Total: %i{result}
"""*)
    let resultName =
         if (kept[0]=6)&&(kept[1]=6) then
            CriticalSuccess
         else if (kept[0]=1)&&(kept[1]=1) then
             CriticalFailure
         else match result with
            | n when  n<7 -> Failure
            | n when n>6 && n<10 -> PartialSuccess
            | n when n>9 -> Success
    let resultColor =
        match resultName with
        | CriticalFailure -> Color.Red
        | Failure ->Color.DarkRed
        | PartialSuccess -> Color.DarkGreen
        | Success -> Color.Green
        | CriticalSuccess -> Color.Gold
                    
    EmbedBuilder(Title=string(resultName),
                   Description = $"Rolled pool of %i{pool.d6}d6%+i{pool.plus}")
        .WithColor(resultColor)
        .AddField("Rolls", $"%A{rolls}")
        .AddField("Kept",$"%A{kept}")
        .AddField("Result",result).Build()
let poolParser = Regex("(\d+)d6([+,-]\d+)?",RegexOptions.Compiled)  
let parse_pool instr:DicePool option =
   let matches:Match = poolParser.Match(instr)
   match matches.Success with
   | false -> None
   | true ->
       let groups = matches.Groups
       let poolDice = int(groups[1].Value)
       match groups[2].Success with
       | false ->
           Some({
            d6=poolDice
            plus=0
           })
       | true ->
           let plus = int(groups[2].Value)
           Some({
            d6=poolDice
            plus=plus
           })
  
      
let do_slash_command (cmd:SocketSlashCommand)  =
    try
        let modifyResponseEmbed embed  =
             cmd.ModifyOriginalResponseAsync(fun props ->
                           props.Embed<-Optional(embed)
             ) |> Async.AwaitTask |> ignore
             
        let modifyResponseMessage msg  =
             cmd.ModifyOriginalResponseAsync(fun props ->
                           props.Content<-Optional(msg)
             ) |> Async.AwaitTask |> ignore
             
        match cmd.Data.Name with
        |  "pool" ->
           match parse_pool(string(cmd.Data.Options.ElementAt(0).Value)) with
           | Some dicePool ->
                   dicePool
                   |>do_roll
                   |> modifyResponseEmbed     
           | None ->
               modifyResponseMessage "ApocalypseForge Error: could not parse pool expression"              
        | _ ->
           modifyResponseMessage "ApocalypseForge Error: Unrecognized command"
    with
    | ex ->
        Console.WriteLine(ex.Message)
     
        
let launch_do_slash_command (cmd: SocketSlashCommand) =
    cmd.DeferAsync()
    |> Async.AwaitTask |>ignore
    async {
        do_slash_command cmd
    }
    |>Async.Start
    Task.CompletedTask

    
let clientReadyCB() : Task =
    client.add_SlashCommandExecuted(launch_do_slash_command)
    let poolOpts =
        SlashCommandOptionBuilder().
            WithName("pooldesc").
            WithType(ApplicationCommandOptionType.String).
            WithDescription("The pool dice to roll").
            WithRequired(true)
    SlashCommandBuilder().
        WithName("pool").
        WithDescription("roll a dice pool specified as nd6+/-m").
        AddOption(poolOpts).
        Build() 
    |> client.CreateGlobalApplicationCommandAsync
    :> Task
    
let connectedCB() : Task =
   // Console.WriteLine("Connected")
    Task.CompletedTask
let connectClient () =
    client.add_Log LogFunc
    
    client.add_Connected(connectedCB)
       
    client.add_Ready(clientReadyCB)

    Async.AwaitTask (client.LoginAsync(TokenType.Bot, token)) |> ignore
    Async.AwaitTask (client.StartAsync()) |> ignore

[<EntryPoint>]    
let main argv =
    connectClient()
    let waitHandles:WaitHandle array  = [|new AutoResetEvent(false)|]
    WaitHandle.WaitAny(waitHandles) |> ignore
    0
   

