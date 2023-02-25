module ApocalypseForge.ApocalypseForge

open System.Threading
open Discord.WebSocket
open System
open System.Text.RegularExpressions
open System.Threading.Tasks
open Discord
open System.Linq
open FSharp.Data

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
    
type ProcessedRoll =
    struct
        val pool:DicePool
        val resultName:RollResult
        val resultColor:Color
        val rolls: int array
        val kept:int array
        val result:int
        new (Pool,ResultName,ResultColor,Rolls,Kept,Result) =
            {pool=Pool;resultName=ResultName;resultColor=ResultColor
             rolls=Rolls;kept=Kept;result=Result}
    end
    
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

type MovesProvider = XmlProvider<Schema="Moves.xsd">
let moves = MovesProvider.Load("Moves.xml").Moves

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

let arrayToCSV numarray =
        let str = $"%A{numarray}"
        str.Substring(2,str.Length-4).Replace(";",",")
        
let do_roll pool  =
    
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
    ProcessedRoll(pool,resultName,resultColor,rolls,kept,result)


let do_pool_embed_func (rollResult:ProcessedRoll) =
     EmbedBuilder()
        .WithTitle("Roll Pool")
        .WithDescription("Roll a dice pool")
        .WithColor(rollResult.resultColor)
        .AddField("Rolls", arrayToCSV(rollResult.rolls))
        .AddField("Kept", arrayToCSV(rollResult.kept))
        .AddField("Result",rollResult.result).Build()
        
let do_move_embed_func (themove:MovesProvider.Move) (rollResult:ProcessedRoll) =
     EmbedBuilder()
        .WithTitle(themove.Name+" "+rollResult.resultName.ToString())
        .WithDescription(themove.Description.XElement.Value)
        .WithColor(rollResult.resultColor)
        .AddField("Rolls", arrayToCSV(rollResult.rolls))
        .AddField("Kept", arrayToCSV(rollResult.kept))
        .AddField("Result",rollResult.result)
        .AddField("Result Description:",
                  match rollResult.resultName with
                  | RollResult.CriticalFailure ->
                      themove.CriticalFailure.XElement.Value
                  | RollResult.Failure ->
                      themove.Failure.XElement.Value
                  | RollResult.PartialSuccess ->
                      themove.PartialSuccess.XElement.Value
                  | RollResult.Success ->
                      themove.FullSuccess.XElement.Value
                  | RollResult.CriticalSuccess ->
                      themove.CriticalSuccess.XElement.Value
                  )
        .Build()        
        

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

let find_move (inp:string) =
    moves
    |> Seq.tryFind(fun move -> move.Name.ToLower().StartsWith(inp.ToLower()))
    
let test_button =
    ComponentBuilder().WithButton("foo","fooid").Build()
let do_slash_command (cmd:SocketSlashCommand)  =
    try
        let modifyResponseEmbed embed  =
             cmd.ModifyOriginalResponseAsync(fun props ->
                           props.Embed<-Optional(embed)
             ) |> Async.AwaitTask |> ignore
             
        let modifyResponseMessage components msg =     
             cmd.ModifyOriginalResponseAsync(fun props ->
                           props.Content<-Optional(msg)
                           match components with
                           Some comp ->
                                props.Components<- Optional(comp)
                           | None -> ()
             ) |> Async.AwaitTask |> ignore
             
        match cmd.Data.Name with
        |  "pool" ->
           match parse_pool(string(cmd.Data.Options.ElementAt(0).Value)) with
           | Some dicePool ->
                   dicePool
                   |>do_roll
                   |> do_pool_embed_func
                   |> modifyResponseEmbed     
           | None ->
               modifyResponseMessage None "ApocalypseForge Error: could not parse pool expression" 
        | "moves" ->
             moves
             |> Seq.fold (fun str move -> str+(move.Name.ToLower())+"\n") ""
             |> modifyResponseMessage None
        | "move" ->
           match parse_pool(string(cmd.Data.Options.ElementAt(1).Value)) with
           | Some dicePool ->
               match find_move(string(cmd.Data.Options.ElementAt(0).Value)) with
               |Some themove ->
                   dicePool
                   |> do_roll
                   |> do_move_embed_func themove
                   |> modifyResponseEmbed
                   ()
               | None ->
                    modifyResponseMessage None ("ApocalypseForge Error: could not match move: "+
                                                string(cmd.Data.Options.ElementAt(0).Value)) 
           | None ->
               modifyResponseMessage None "ApocalypseForge Error: could not parse pool expression" 
        | _ ->
           modifyResponseMessage None "ApocalypseForge Error: Unrecognized command" 
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

let add_slash_command cmdstr descr cmdoptions = 
    try
        let slashCmdBldr=
            SlashCommandBuilder().WithName(cmdstr).
                WithDescription(descr)
        match cmdoptions with
        | Some opts ->
            opts
            |>Seq.iter(fun opt -> slashCmdBldr.AddOption(opt)|>ignore )
            |> ignore
        | None -> ()
        client.CreateGlobalApplicationCommandAsync(slashCmdBldr.Build())
        |> Async.AwaitTask |> ignore
        Task.CompletedTask
    with 
    | ex ->
        Console.WriteLine(ex.Message)
        Console.WriteLine(ex.StackTrace)
        Task.CompletedTask

let make_string_option_list  optionTuples =
    optionTuples
    |> Seq.map(fun otuple ->
        SlashCommandOptionBuilder().
            WithName(fst otuple).
            WithType(ApplicationCommandOptionType.String).
            WithDescription(snd otuple).
            WithRequired(true))
    
let clientReadyCB() : Task =
    client.add_SlashCommandExecuted(launch_do_slash_command)
    add_slash_command "pool" "Roll a dice pool"
        (Some (make_string_option_list
                   [|
                       ("dice_expression","The pool in the form Nd6+M")
                   |])) 
    |> Async.AwaitTask |> ignore
    add_slash_command "moves" "List all the moves" None
    |> Async.AwaitTask |> ignore
    add_slash_command "move" "Make a move"
        (Some (make_string_option_list
                   [|
                       ("move_name","The name of the move")
                       ("dice_expression","The pool in the form Nd6+M")
                   |])) 
    
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
   

