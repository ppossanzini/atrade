# Provenance - cTrader Open API protobuf schema

Vendored, not hand-written. Do not edit these files: any change would break the correspondence with
the published protocol.

| Field | Value |
| --- | --- |
| Source repository | https://github.com/spotware/openapi-proto-messages |
| Commit | `3fd8bddfbe0cfc2ecfda079623dc4e498af11e66` |
| License | MIT (see `LICENSE.cTrader-proto-messages`) |
| Retrieved on | 2026-09-18 |
| Files | `OpenApiCommonMessages.proto`, `OpenApiCommonModelMessages.proto`, `OpenApiMessages.proto`, `OpenApiModelMessages.proto` |

Notes:

- The schema declares `syntax = "proto2"` and no `package`, so protoc would place the generated types
  in the global namespace. Each vendored file therefore carries one additive line that is **not**
  part of the upstream file:

  ```proto
  option csharp_namespace = "AutoTrade.Trading.Handlers.Broker.Protocol";
  ```

  It must be re-applied after re-vendoring; `--csharp_opt=base_namespace` is not an alternative,
  because protoc rejects it when the file declares no namespace (verified on 2026-09-18).
- The .NET SDK published by the same vendor (`cTrader.OpenAPI.Net` 1.4.4, 2022-05-03) targets `net6.0`
  and pins `Google.Protobuf` 3.20.1; the schema repository is maintained and was updated on
  2025-11-13. The project therefore generates messages from the schema with a current toolchain
  instead of consuming the stale package.

To update:

1. pick the new commit and replace the four files from that commit;
2. re-apply the `option csharp_namespace` line to each file;
3. update the commit field in this document;
4. run `dotnet build server/AutoTrade.Trading.Handlers/AutoTrade.Trading.Handlers.csproj` and fix any
   contract breakage deliberately, never silently.
