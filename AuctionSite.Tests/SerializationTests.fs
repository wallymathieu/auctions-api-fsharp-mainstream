module AuctionSite.Tests.SerializationTests

open System
open System.IO
open System.Text.Json
open Expecto
open Expecto.Flip
open AuctionSite.Money
open AuctionSite.Domain
open AuctionSite.Tests.SampleData
open AuctionSite.Persistence.JsonFile

let scratchDirectory = "./.tmp"

let vac0 = createAmount Currency.VAC 0L
let timedAscending = TimedAscending { 
    ReservePrice = 0
    MinRaise = 0
    TimeFrame = TimeSpan.Zero 
}

let addAuction = AddAuction(sampleStartsAt, sampleAuction)
let bid = PlaceBid(sampleBidTime, bid1)

let noWarn (_: string) (_: string) (_: exn) = ()

let withScratchFile (body: string -> Async<unit>) = async {
    Directory.CreateDirectory scratchDirectory |> ignore
    let path = Path.Combine(scratchDirectory, $"serialization-test-{Guid.NewGuid():N}.jsonl")
    try
        do! body path
    finally
        File.Delete path
}

let writeSampleCommands (path: string) =
    File.WriteAllLines(path, [
        JsonSerializer.Serialize(addAuction, Serialization.serializerOptions())
        JsonSerializer.Serialize(bid, Serialization.serializerOptions())
    ])

[<Tests>]
let serializationTests = testList "Serialization Tests" [
    testList "Serialization" [
        test "Can deserialize AuctionType" {
            let json = "\"English|0|0|0\""
            let deserializedOption = 
                match AuctionType.TryParse <| json.Trim('"') with
                | Some t -> t
                | None -> failwith "Failed to parse auction type"
                
            deserializedOption |> Expect.equal "Deserialized auction type should match expected" timedAscending
        }
        
        test "Can parse Amount" {
            let amountStr = "VAC0"
            let amount = tryParseAmount amountStr
            
            amount |> Expect.equal "Parsed amount should match expected" (Some vac0)
        }
        
        test "Can serialize and deserialize Amount" {
            let amount = vac0
            let serialized = string amount
            let deserialized = tryParseAmount serialized
            
            deserialized |> Expect.equal "Deserialized amount should match original" (Some amount)
        }
        
        test "Can serialize and deserialize AuctionType" {
            let auctionType = timedAscending
            let serialized = auctionType.ToString()
            let deserialized = AuctionType.TryParse serialized
            
            deserialized |> Expect.equal "Deserialized auction type should match original" (Some auctionType)
        }
        
    ]
    
    testList "File Operations" [
        testAsync "Can read commands from JSON file" {
            do! withScratchFile (fun sampleCommandsFile -> async {
                writeSampleCommands sampleCommandsFile
                let! commandsResult = readCommands noWarn sampleCommandsFile

                commandsResult |> Expect.isSome "Should be able to read commands"
                match commandsResult with
                | Some commands ->
                    Expect.isTrue "Should have at least one command" (commands.Length > 0)
                | None ->
                    failtest "Failed to read commands"
            })
        }

        testAsync "Can write and read commands" {
            do! withScratchFile (fun sampleCommandsFile -> withScratchFile (fun writtenCommandsFile -> async {
                writeSampleCommands sampleCommandsFile
                let! commandsOption = readCommands noWarn sampleCommandsFile
                let commands =
                    match commandsOption with
                    | Some cmds -> cmds
                    | None -> failwith "Failed to read sample commands"

                // Split commands and write them in two parts
                let commands1, commands2 =
                    match commands with
                    | [] -> [], []
                    | [cmd] -> [cmd], []
                    | cmd::rest -> [cmd], rest

                do! writeCommands writtenCommandsFile commands1
                do! writeCommands writtenCommandsFile commands2

                let! readCommandsOption = readCommands noWarn writtenCommandsFile
                match readCommandsOption with
                | Some readCmds ->
                    readCmds.Length |> Expect.equal "Read commands count should match original" commands.Length
                | None ->
                    failtest "Failed to read written commands"
            }))
        }

        testAsync "Can read and write events" {
            do! withScratchFile (fun eventsFile -> async {
                let events = [
                    AuctionAdded(sampleStartsAt, sampleAuction)
                    BidAccepted(sampleBidTime, bid1)
                ]

                do! writeEvents eventsFile events

                let! readEventsOption = readEvents noWarn eventsFile
                match readEventsOption with
                | Some readEvents ->
                    readEvents.Length |> Expect.equal "Read events count should match original" events.Length
                | None ->
                    failtest "Failed to read written events"
            })
        }
        
        testAsync "Load events creates the missing directory and finds no events" {
            let dir = Path.Combine(".tmp", "load-events", string (Guid.NewGuid()))
            let! events = loadEvents noWarn (Path.Combine(dir, "events.jsonl"))
            events |> Expect.isEmpty "no events"
            Directory.Exists dir |> Expect.isTrue "directory created"
        }

        testAsync "Load events reads the stored events" {
            let path = Path.Combine(".tmp", "load-events", string (Guid.NewGuid()), "events.jsonl")
            let stored = [ AuctionAdded(sampleStartsAt, sampleAuction); BidAccepted(sampleBidTime, bid1) ]
            let! _ = loadEvents noWarn path
            do! writeEvents path stored
            let! events = loadEvents noWarn path
            events |> Expect.equal "stored events" stored
        }

        test "Can serialize and deserialize AddAuction command" {
            let serialized = JsonSerializer.Serialize(addAuction, Serialization.serializerOptions())
            
            // Check that the serialized string contains expected parts
            serialized |> Expect.stringContains "Serialized string should contain AddAuction case" "\"Case\":\"AddAuction\""
            let deSerialized = JsonSerializer.Deserialize<Command>(serialized, Serialization.serializerOptions())
            deSerialized |> Expect.equal "Deserialized command should match original" addAuction
        }

        test "Can serialize PlaceBid command" {
            let serialized = JsonSerializer.Serialize(bid, Serialization.serializerOptions())
            
            // Check that the serialized string contains expected parts
            serialized |> Expect.stringContains "Serialized string should contain PlaceBid case" "\"Case\":\"PlaceBid\""
            let deSerialized = JsonSerializer.Deserialize<Command>(serialized, Serialization.serializerOptions())
            deSerialized |> Expect.equal "Deserialized command should match original" bid
        }
    ]
]
