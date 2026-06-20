namespace AuctionSite.Persistence

open System.IO
open System.Text.Json
open AuctionSite.Domain

/// Module for JSON file persistence
module JsonFile =
    
    /// Generic read function for any JSON-decodable type
    let readJsonFile<'T> (warn: string -> exn -> unit) (path: string) : Async<'T list option> = async {
        if not (File.Exists path) then
            return None
        else
            let! content = File.ReadAllLinesAsync(path) |> Async.AwaitTask
            let items =
                content
                |> Array.choose (fun line ->
                    try
                        Some(JsonSerializer.Deserialize<'T>(line, Serialization.serializerOptions()))
                    with
                    | :? JsonException as ex ->
                        warn (sprintf "Skipping malformed JSON line in '%s': %s" path line) ex
                        None)
                |> Array.toList
            return Some items
    }
    
    /// Generic write function for any JSON-encodable type
    let writeJsonFile<'T> (path: string) (items: 'T list) : Async<unit> = async {
        let serialized = 
            items 
            |> List.map (fun item -> JsonSerializer.Serialize(item, Serialization.serializerOptions()))
            
        let exists = File.Exists path
        
        if not exists then
            do! File.WriteAllLinesAsync(path, serialized) |> Async.AwaitTask
        else
            use fileStream = new FileStream(path, FileMode.Append)
            use writer = new StreamWriter(fileStream)
            
            for line in serialized do
                do! writer.WriteLineAsync(line) |> Async.AwaitTask
    }
    
    /// Read commands from a JSON file
    let readCommands (warn: string -> exn -> unit) (path: string) : Async<Command list option> =
        readJsonFile<Command> warn path

    /// Write commands to a JSON file
    let writeCommands (path: string) (commands: Command list) : Async<unit> =
        writeJsonFile<Command> path commands

    /// Read events from a JSON file
    let readEvents (warn: string -> exn -> unit) (path: string) : Async<Event list option> =
        readJsonFile<Event> warn path

    /// Write events to a JSON file
    let writeEvents (path: string) (events: Event list) : Async<unit> =
        writeJsonFile<Event> path events
