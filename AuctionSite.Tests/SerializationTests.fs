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

// Test setup and helper functions
let tmpSampleCommands = "./tmp/test-sample-commands.jsonl"
let sampleCommandsFile = "./test-samples/sample-commands.jsonl"

let vac0 = createAmount Currency.VAC 0L
let timedAscending = TimedAscending { 
    ReservePrice = vac0
    MinRaise = vac0
    TimeFrame = TimeSpan.Zero 
}

let addAuction = AddAuction(sampleStartsAt, sampleAuction)
let bid = PlaceBid(sampleBidTime, bid1)

// Helper function to set up the temp directory for tests
let setupTempDirectory() =
    let dir = Path.GetDirectoryName(tmpSampleCommands)
    if not (Directory.Exists(dir)) then
        Directory.CreateDirectory(dir) |> ignore
        
    if File.Exists(tmpSampleCommands) then
        File.Delete(tmpSampleCommands)

// Helper function to set up sample files
let setupSampleFiles() =
    setupTempDirectory()
    
    // Create test samples directory and sample file if it doesn't exist
    let testSamplesDir = Path.GetDirectoryName(sampleCommandsFile)
    if not (Directory.Exists(testSamplesDir)) then
        Directory.CreateDirectory(testSamplesDir) |> ignore
        
    if not (File.Exists(sampleCommandsFile)) then
        // Create some sample commands
        let commands = [
            JsonSerializer.Serialize(addAuction, Serialization.serializerOptions())
            JsonSerializer.Serialize(bid, Serialization.serializerOptions())
        ]
        File.WriteAllLines(sampleCommandsFile, commands)

let serializationTests = testList "Serialization Tests" [
    testList "Serialization" [
        test "Can deserialize AuctionType" {
            let json = "\"English|VAC0|VAC0|0\""
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
        testCase "Setup" (fun _ -> setupSampleFiles())
        
        testAsync "Can read commands from JSON file" {
            let! commandsResult = readCommands sampleCommandsFile
            
            commandsResult |> Expect.isSome "Should be able to read commands"
            match commandsResult with
            | Some commands ->
                Expect.isTrue (commands.Length > 0) "Should have at least one command"
            | None -> 
                failtest "Failed to read commands"
        }
            
        testAsync "Can write and read commands" {
            // Get sample commands
            let! commandsOption = readCommands sampleCommandsFile
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
                
            do! writeCommands tmpSampleCommands commands1
            do! writeCommands tmpSampleCommands commands2
            
            // Read back the commands
            let! readCommandsOption = readCommands tmpSampleCommands
            match readCommandsOption with
            | Some readCmds ->
                // Verify all commands were written and read correctly
                readCmds.Length |> Expect.equal "Read commands count should match original" commands.Length
            | None ->
                failtest "Failed to read written commands"
        }
        
        testAsync "Can read and write events" {
            // Create sample events
            let events = [
                AuctionAdded(sampleStartsAt, sampleAuction)
                BidAccepted(sampleBidTime, bid1)
            ]
            
            // Write events
            do! writeEvents tmpSampleCommands events
            
            // Read back the events
            let! readEventsOption = readEvents tmpSampleCommands
            match readEventsOption with
            | Some readEvents ->
                // Verify all events were written and read correctly
                readEvents.Length |> Expect.equal "Read events count should match original" events.Length
            | None ->
                failtest "Failed to read written events"
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
