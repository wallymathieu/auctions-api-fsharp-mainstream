module AuctionSite.Tests.SerializationTests

open System
open System.IO
open System.Text.Json
open AuctionSite.Money
open NUnit.Framework
open FsUnit
open AuctionSite.Domain
open AuctionSite.Tests.SampleData
open AuctionSite.Persistence.JsonFile

[<TestFixture>]
type SerializationTests() =
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
    
    [<SetUp>]
    member _.Setup() =
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
    
    [<Test>]
    member _.``Can deserialize AuctionType``() =
        let json = "\"English|VAC0|VAC0|0\""
        let deserializedOption = 
            match AuctionType.TryParse <| json.Trim('"') with
            | Some t -> t
            | None -> failwith "Failed to parse auction type"
            
        deserializedOption |> should equal timedAscending
        
    [<Test>]
    member _.``Can parse Amount``() =
        let amountStr = "VAC0"
        let amount = tryParseAmount amountStr
        
        amount |> should equal (Some vac0)
        
    [<Test>]
    member _.``Can serialize and deserialize Amount``() =
        let amount = vac0
        let serialized = string amount
        let deserialized = tryParseAmount serialized
        
        deserialized |> should equal (Some amount)
        
    [<Test>]
    member _.``Can serialize and deserialize AuctionType``() =
        let auctionType = timedAscending
        let serialized = auctionType.ToString()
        let deserialized = AuctionType.TryParse serialized
        
        deserialized |> should equal (Some auctionType)
        
    [<Test>]
    member _.``Can read commands from JSON file``() =
        let commandsResult = (readCommands sampleCommandsFile) |> Async.RunSynchronously
        
        commandsResult |> should not' (equal None)
        match commandsResult with
        | Some commands ->
            commands.Length |> should be (greaterThan 0)
        | None -> 
            Assert.Fail("Failed to read commands")
            
    [<Test>]
    member _.``Can write and read commands``() = async {
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
            readCmds.Length |> should equal commands.Length
        | None ->
            Assert.Fail("Failed to read written commands")
    }
    
    [<Test>]
    member _.``Can read and write events``() = async {
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
            readEvents.Length |> should equal events.Length
        | None ->
            Assert.Fail("Failed to read written events")
    }
    //
    [<Test>]
    member _.``Can serialize and deserialize AddAuction command``() =
        let serialized = JsonSerializer.Serialize(addAuction, Serialization.serializerOptions())
        
        // Check that the serialized string contains expected parts
        serialized |> should contain "\"Case\":\"AddAuction\""
        let deSerialized = JsonSerializer.Deserialize<Command>(serialized, Serialization.serializerOptions())
        Assert.That(deSerialized, Is.EqualTo(addAuction))

    [<Test>]
    member _.``Can serialize PlaceBid command``() =
        let serialized = JsonSerializer.Serialize(bid, Serialization.serializerOptions())
        
        // Check that the serialized string contains expected parts
        serialized |> should contain "\"Case\":\"PlaceBid\""
        let deSerialized = JsonSerializer.Deserialize<Command>(serialized, Serialization.serializerOptions())
        Assert.That(deSerialized, Is.EqualTo(bid))
