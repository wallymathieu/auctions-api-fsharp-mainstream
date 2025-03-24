namespace AuctionSite.Domain
module Serialization =
    open AuctionSite
    open AuctionSite.Domain
    open System.Text.Json
    open System.Text.Json.Serialization

    let private converters : JsonConverter list = [
        UserJsonConverter()
        AmountJsonConverter()
        JsonStringEnumConverter()
        JsonFSharpConverter(JsonFSharpOptions.Default())
    ]
    let serializerOptions() =
        let opts = JsonSerializerOptions()
        opts.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
        opts.WriteIndented <- false
            
        for converter in converters do
            opts.Converters.Add(converter)
        opts