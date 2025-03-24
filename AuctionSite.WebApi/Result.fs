namespace AuctionSite.WebApi

/// Computation expression for working with Result<'T, 'E>
[<AutoOpen>]
module ResultCE =
    type ResultBuilder() =
        member _.Return(x) = Ok x
        member _.ReturnFrom(m: Result<'T, 'E>) = m
        member _.Bind(m: Result<'T, 'E>, f: 'T -> Result<'U, 'E>) : Result<'U, 'E> =
            match m with
            | Ok a -> f a
            | Error e -> Error e
        member _.Zero() = Ok()
        member _.Delay(f) = f
        member _.Run(f) = f()
        member this.Combine(m, f) = this.Bind(m, f)
        member _.TryWith(m, h) =
            try
                m()
            with
            | exn -> h exn
        member _.TryFinally(m, comp) =
            try
                m()
            finally
                comp()
        member _.Using(res:#System.IDisposable, body) =
            try
                body res
            finally
                if not (isNull (box res)) then
                    res.Dispose()
        member this.While(guard, body) =
            if not (guard()) then
                this.Zero()
            else
                this.Bind(body(), fun () -> this.While(guard, body))
        member this.For(sequence: seq<'T>, body: 'T -> Result<unit, 'E>) =
            this.Using(sequence.GetEnumerator(), fun enum ->
                this.While(enum.MoveNext,
                    fun () -> body enum.Current))
                    
    let result = ResultBuilder()