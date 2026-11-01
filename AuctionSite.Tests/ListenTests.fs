module AuctionSite.Tests.ListenTests

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Expecto
open Expecto.Flip
open AuctionSite.WebApi

let private configuredUrls (args: string array) =
    let builder = WebApplication.CreateBuilder(args)
    Listen.configure builder
    builder.WebHost.GetSetting(WebHostDefaults.ServerUrlsKey)

[<Tests>]
let listenTests =
    testList "Listen URLs" [
        test "Falls back to http://localhost:8080 when no urls are configured" {
            Listen.urls null
            |> Expect.equal "default url" "http://localhost:8080"
        }

        test "Falls back to http://localhost:8080 when the configured urls are blank" {
            Listen.urls "  "
            |> Expect.equal "default url" "http://localhost:8080"
        }

        test "Keeps the configured urls" {
            Listen.urls "http://localhost:9092;http://localhost:9093"
            |> Expect.equal "configured urls" "http://localhost:9092;http://localhost:9093"
        }

        test "Binds the host to http://localhost:8080 when the configured urls are blank" {
            configuredUrls [| "--urls"; "" |]
            |> Expect.equal "urls setting" "http://localhost:8080"
        }

        test "Honours the --urls command-line argument" {
            configuredUrls [| "--urls"; "http://localhost:9091" |]
            |> Expect.equal "urls setting" "http://localhost:9091"
        }
    ]
