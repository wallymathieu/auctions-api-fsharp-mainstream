namespace AuctionSite

// Equivalent to AuctionSite.Money.hs
module Money =
    /// Currency types supported by the system
    type Currency =
        | VAC  // Virtual auction currency
        | SEK  // Swedish Krona
        | DKK  // Danish Krone
        
    /// Represents a monetary amount with a specific currency
    type Amount = { 
        Currency: Currency 
        Value: int64
    }
    
    /// Creates a new amount of specified currency and value
    let createAmount currency value = { Currency = currency; Value = value }
    
    /// VAC currency amount shorthand
    let vac value = createAmount VAC value
    
    /// SEK currency amount shorthand
    let sek value = createAmount SEK value
    
    /// DKK currency amount shorthand
    let dkk value = createAmount DKK value
    
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
            
    /// Format an amount as a string
    let toString amount = sprintf "%A%d" amount.Currency amount.Value
    
    /// Parse an amount from a string
    let tryParse (s: string) =
        // Example format: "VAC10" or "SEK100"
        let currencyStr = new string(s |> Seq.takeWhile System.Char.IsLetter |> Seq.toArray)
        let valueStr = new string(s |> Seq.skipWhile System.Char.IsLetter |> Seq.toArray)
        
        match currencyStr with
        | "VAC" -> 
            match System.Int64.TryParse valueStr with
            | true, value -> Some (vac value)
            | _ -> None
        | "SEK" -> 
            match System.Int64.TryParse valueStr with
            | true, value -> Some (sek value)
            | _ -> None
        | "DKK" -> 
            match System.Int64.TryParse valueStr with
            | true, value -> Some (dkk value)
            | _ -> None
        | _ -> None
