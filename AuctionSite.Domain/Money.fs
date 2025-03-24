namespace AuctionSite
open System
open System.Globalization
open System.Text.RegularExpressions
module Money =
    /// Currency types supported by the system
    type Currency =
        | VAC = 0  // Virtual auction currency
        | SEK = 752 // Swedish Krona
        | DKK = 208 // Danish Krone
    module Currency=
        let tryParse (s:string) : Currency option =
            match Enum.TryParse<Currency>(s) with
            | false, _ -> None
            | true, v -> Some v

    /// Represents a monetary amount with a specific currency
    type Amount = { 
        Currency: Currency 
        Value: int64
    } with
        override this.ToString() = sprintf "%A%d" this.Currency this.Value

    let private amountRegex = Regex("(?<currency>[A-Z]+)(?<value>[0-9]+)")

    /// Creates a new amount of specified currency and value
    let createAmount currency value = { Currency = currency; Value = value }
    
    /// Gets the currency of an amount
    let amountCurrency amount = amount.Currency
    
    /// Gets the value of an amount
    let amountValue amount = amount.Value
    
    /// Add two amounts of the same currency
    let add a1 a2 = 
        if a1.Currency = a2.Currency then
            { Currency = a1.Currency; Value = a1.Value + a2.Value }
        else
            failwith "Cannot add two amounts with different currencies"
    
    /// Check if one amount is greater than another
    let isGreaterThan a1 a2 =
        if a1.Currency = a2.Currency then
            a1.Value > a2.Value
        else
            failwith "Cannot compare two amounts with different currencies"
    
    /// Parse an amount from a string
    let tryParseAmount (s: string) =
        let m = amountRegex.Match(s)
        if m.Success then
            let currencyString = m.Groups["currency"].Value;
            let v = m.Groups["value"].Value
            match Currency.tryParse(currencyString) with
            | Some currency -> Some { Value= Int64.Parse(v, CultureInfo.InvariantCulture); Currency= currency}
            | None -> None
        else None

open System
open System.Text.Json.Serialization
open System.Text.Json
open Money

/// JSON converter for the Amount type
type AmountJsonConverter() =
    inherit JsonConverter<Amount>()
    
    /// Serialize a Amount object to JSON
    override _.Write(writer: Utf8JsonWriter, amount: Amount, options: JsonSerializerOptions) =
        writer.WriteStringValue(amount.ToString())
    
    /// Deserialize a Amount object from JSON
    override _.Read(reader: byref<Utf8JsonReader>, _: Type, options: JsonSerializerOptions) =
        let amountString = reader.GetString()
        match tryParseAmount amountString with
        | Some amount -> amount
        | None -> failwith $"Invalid amount format: %s{amountString}"
