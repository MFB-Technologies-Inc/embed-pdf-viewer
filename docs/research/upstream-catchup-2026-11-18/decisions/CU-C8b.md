# CU-C8b decisions

## CU-C8b-D1. The browser transport posts one attempt and leaves the rest to Rust

Decision: `fetchTransport()` (`packages/core-wasm/src/signature-transport.ts`) does what one `ask`
in upstream's `httpTimestampAuthority` and `httpRevocationSource` does at the network: a POST with
`content-type` and then the request's headers, an `AbortController` that fires after `timeoutMs`
and covers reading the reply, and for a failed timestamp query in a page the clients' hint about
calling the authority through a route. Every status comes back as `replied`. Retries, the 250 ms
delay, what a status means and the error texts stay in the Rust clients.
Reason: CU-C4b-D2 put those in Rust, and the Rust client already words each outcome
(`crates/plugins/signature-core/src/transport.rs` `attempt()`). Duplicating the status mapping in
JavaScript would give two places to drift.
Cost: the transport reads the body of a 5xx or 429 that upstream's timestamp client never reads.
It is bounded by `maxReplyBytes`, so the cost is a read, not a risk.
Reverse: not intended.

## CU-C8b-D2. The transport stops reading at `maxReplyBytes`

Decision: a reply whose `content-length` is over `maxReplyBytes`, or whose body passes it while
being read, is cancelled and answers `tooLarge`, as the routes' `readUpTo` does
(`packages/core/signature/src/server/revocation-route.ts`). A body that breaks off answers `failed`
with `the reply broke off: <message>`, or `timedOut` when the abort caused it.
Reason: the brief (CU-C4b-D3's limits), and the host has to enforce them: the Rust client checks a
`replied` body's length only after the host has read all of it (`7b8f20ecd`).
Cost: upstream's browser clients set no limit, so a route that answers more than 1 MiB for a
timestamp (2 MiB for revocation) works upstream and is `tooLarge` here. Upstream's own routes never
pass more than that. Listed beside CU-C4b-D3's exception.
Reverse: pass `u32::MAX` from the Rust clients (CU-C4b-D3's reverse).

## CU-C8b-D3. `core-wasm` exports `httpTimestampAuthority` and `httpRevocationSource` by upstream's names

Decision: both take upstream's options and answer `{ ...options, transport: fetchTransport({ fetch }) }`,
the config shape the signature adapter reads (CU-C8a-D4), typed as upstream's return type.
Reason: an app moving from the TypeScript plugin to the Rust one changes the import and nothing
else, the way `src/plugins.ts` mirrors the plugin factories.
Cost: the value is a description, not a `CmsSigner` or `RevocationSource`. Handed to the
TypeScript plugin, or called as a signer, it fails. The cast hides that from the type checker.
Reverse: a distinct type and name (`rustTimestampAuthority`), at the cost of the swap-the-import
property.

## CU-C8b-D4. The granted-timestamp test echoes the nonce by patching pkijs in the test

Decision: `test/signature-transport.test.ts` signs through the real `timestampRoute` with
`createTestTimestampAuthority()` behind it. That authority writes no nonce
(`packages/core/signature/src/cms/timestamp.ts` `buildTimestampToken`), so while the stub authority
answers, the test wraps `pkijs.TSTInfo.prototype.toSchema` to set the nonce read from the query.
`@embedpdf/core-signature`, `pkijs` and `asn1js` are `core-wasm` dev dependencies; the lockfile
gains nine lines and `pkijs` resolves to the same install `core-signature` uses.
Reason: CU-C8a-D8 leaves a computed reply as the only way to get a granted token past the client's
nonce check, and the patch is ten lines where a copy of `buildTimestampToken` would be seventy.
Cost: the test depends on one `pkijs` instance and on `buildTimestampToken` encoding through
`TSTInfo.toSchema`. An upstream change to either breaks the test, not the transport.
Reverse: give `buildTimestampToken` a `nonce` input upstream, or copy it into the test.

## CU-C8b-D5. linux-x64 ran in a temporary amd64 container under Rosetta

Decision: the linux-x64 .NET rows ran in container `cu-c8b-x64`, made from
`mcr.microsoft.com/dotnet/sdk:10.0.403` (amd64, image
`sha256:3386778a73b501d3918c3dc26685bd284aa632ca06f2f99e6bf642a3b325334b`) with the job volume
mounted, `python3`, `binutils`, `gcc` and `libclang-dev` from apt, and Rust 1.98.0 in
`/workspaces/x64`. `validate.sh` ran with `EPDF_EMULATED=true`, so the package manifest records
`container-emulated`. The container, its image and `/workspaces/x64` were removed afterwards.
Reason: `validate.sh` refuses a RID other than the host's (`packaging/package.py` `smoke`), and
`pack.sh` runs `ldd` on the library, which a foreign-architecture `.so` fails. There is no amd64
build of `embedpdf-dev`; building one is about 12.6 GB.
Cost: it is not the project's approved container, and emulation is not native evidence, as the
2026-11-02 slice 6 linux-x64 record also says.
Reverse: an amd64 build of the dev image, or a native x64 Linux host.

## CU-C8b-D6. osx-x64 ran under Rosetta and its record step fails

Decision: on the Mac, `dotnet.sh` ran for `osx-x64` from `arch -x86_64 /bin/bash` with an x64
.NET SDK 10.0.403 and `/usr/bin/python3`, without the facade tests (an x64 `dotnet test` cannot
load the arm64 debug library). The relocated consumer smoke passed. `validate.sh` then exits 1:
`$.rids.osx-x64.status: unsupported value 'container-emulated'`. It is reported as that, not rerun
without `EPDF_EMULATED`.
Reason: without the flag the tooling writes `VALIDATED` with "matching native architecture",
which a Rosetta run is not. The schema has no emulated status for macOS, and `platforms/dotnet` is
not this job's to change.
Cost: the osx-x64 row has a passing smoke and a failing exit code.
Reverse: an emulated status for osx-x64 in `package-manifest.schema.json`, or a native Intel Mac.

## CU-C8b-D7. .NET has no timestamp or long-term row

Decision: the .NET rows are regression only. The plan's done line (a timestamped signature and a
long-term fill per platform) is not applicable to .NET.
Reason: CU-C8a-D6. `epdf-capi` exposes no signing, so there is nothing to timestamp or fill.
Cost: the plan's per-platform claim holds for the browser only in this job.
Reverse: CU-C8a-D6's reverse.
