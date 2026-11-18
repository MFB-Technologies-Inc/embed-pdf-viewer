# CU-C9: Swift on the Mac

`research/catchup-2026-11-18-swift`, cut from `research/catchup-2026-11-18-c8c` at `72287b00c`,
builds the four Apple fork slices for runtime `af480d0ce`, packages the engine framework locally,
and gives the Swift package every CU-C8a surface and a D-1 transport on `URLSession`. The three
Swift test runs pass at head: 519 of 519 Swift Testing tests on the iOS simulator, macOS arm64 and
macOS x86_64, where `cf7fdbe7a` ran 506 (and failed one UI test on x86_64). The API coverage check
reports the same 18 pre-existing errors as `cf7fdbe7a`; the four members this catch-up added are
mapped. Nothing was published and neither `Package.swift` pin moved. Decisions are in
[`../decisions/CU-C9.md`](../decisions/CU-C9.md).

## Commits

| Commit | Summary |
|---|---|
| `e07c4b00e` | the Swift surfaces, `SignatureTransport.swift`, `swift-api-coverage.json`, a README note |
| `6e39c46c6` | `SignatureTransportTests.swift`, `LoopbackServer.swift`, the builder row, `DisabledPluginTests` |
| the docs commit | this report, the decisions, the `decisions.md` index, `scripts/cu-c9/` |

## Slices and framework

`scripts/cu-c9/slices.mjs` calls `perf-compare/scripts/build-arm.mjs`'s `buildFork`, the function
`publish-engine-payload.mjs` uses, for the four targets, in a fork tree seeded from the cached
`17d8073d4` one (CU-C9-D1). Every `BUILD-METADATA.json` names `af480d0ce420ed916db0de8d2e8827822ad7be1e`.

| Target | Build | `libembedpdf.a` sha256 | `EPDFDss_*` |
|---|---|---|---|
| `ios-arm64` | 63 s | `6fd3e22346f4345cb6860dad464ad6ce7138d063f0361a13c09e18694f1e703f` | 3 |
| `ios-sim-arm64` | 58 s | `d83f9220bc03f29e74bbd1bc0c130f9c48dc5d5300d65d1bda88e889032fdc46` | 3 |
| `darwin-static-arm64` | 56 s | `6433908502bcdfa5014a55aa547eec5f3656043e6c4bc8888b29854c0275cbd5` | 3 |
| `darwin-static-x64` | 77 s | `eac2064cf3686093948ad697c88a1dcf5c7ed84387fb897e37a2e5de6819fe89` | 3 |

The three are `EPDFDss_Add`, `EPDFDss_GetCount` and `EPDFDss_GetData` (CU-C1's list), read with the
Rust toolchain's `llvm-nm -gUj`; the `17d8073d4` `ios-arm64` slice has none. `build-xcframework.sh`
(`LIB_ROOT` at the staging root, `EMBEDPDF_NM` set, `CARGO_BUILD_JOBS=8`) took 16 min 20 s and
passed its native graph check. `package-engine-framework.sh` wrote
`EmbedPDFBindings.xcframework.zip`, 114,485,572 bytes, SwiftPM checksum
`6d04a49d8859b715a85ce2b4d767ee932838f41e43054e68330c67a9b4572470`; each of its three
`EmbedPDFBindings` binaries holds the three `EPDFDss_*` symbols per architecture.
`apple-engine-hash.sh` gives `eda11a35f09a0134` for this tree, the release CU-C10 would publish.
`APPLE_CRATES` matches `cargo metadata`'s crates under `epdf-viewer-host` (none missing, none
extra), so it is unchanged.

## Swift API added

Aliases: `SignatureRequestedTime` (beside `SignatureValidationTime`), `SignatureCallAuthority`,
`SignatureTimestampAuthority`, `SignatureRevocationRoute`, `SignatureTransport`,
`SignatureTransportRequest`, `SignatureTransportHeader`, `SignatureTransportCompletion`,
`SignatureRevocationStatus`, `SignatureTimestampVerdict`, `SignatureLongTermVerdict`,
`SignatureLongTermGap`, `SignatureLongTermGapReason`, `SignatureValidationInfoAdded`,
`SignatureValidationInfoCounts`, `SignatureFailure`, `StandardCommandList`,
`StandardCommandsPlatform`, `StandardCommandCompletion`, `ToolPermission`.

```swift
SignatureCapability.addValidationInfo(_ field: FormFieldRef? = nil) async throws(PDFError) -> SignatureValidationInfoAdded
SignatureCapability.canAddValidationInfo() async throws(PDFError) -> Bool
PDFViewer.setSignatureTransport(_: (any SignatureTransport)?)
PDFViewer.setCommandsPlatform(_: (any StandardCommandsPlatform)?)
ViewerConfigBuilder.standardCommands(_ lists: [StandardCommandList]) -> ViewerConfigBuilder
InteractionCapability.registerTool(_:replace:permission: (any ToolPermission)? = nil)
InteractionCapability.canUseTool(_ id: ToolID) async throws(PDFError) -> Bool
AnnotationDraftsCapability.hasCreationAwaitingClick() async throws(PDFError) -> Bool
StandardCommandCompletion.fail(_ error: any Error)
public final class URLSessionSignatureTransport: SignatureTransport  // init(session: URLSession = .shared)
```

The settings fields, the per-call authority, the verdict fields and `StampImportOptions.id` arrive
through the generated records the aliases already expose. No Swift caller set `validationTime`, so
nothing broke on CU-C8a-D9. `URLSessionSignatureTransport` makes one attempt per `post`, holds it to
`timeoutMs` from connect to the last byte, answers `tooLarge` past `maxReplyBytes` on a 2xx, answers
every status `replied`, and answers a non-2xx whose body runs long, breaks off or is still arriving
at the deadline with that status and no body (CU-C9-D2, CU-C9-D3). The first answer is the only one.

## Tests

`SignatureTransportTests`, 13 tests. Against a loopback `NWListener`: `replied` for 201 and 404 with
the POST, the body, `content-type` then the headers (a later one wins); `timedOut` on a held reply
and a stalled body, each between 250 and 1,400 ms for a 300 ms limit; a 429 stalled at the deadline
is `replied 429` with no body; `tooLarge` by `Content-Length` and while streaming; exactly 1,000
bytes for a 1,000 limit is a reply; a long 503 and a broken-off 503 are `replied 503` with no body;
a broken-off 200 is `failed`; `failed` with nothing listening and for a non-URL; each held exchange
answered once after its reply finished arriving; one `post` through the generated completion from a
viewer. Through upstream's routes (CU-C9-D4): a granted timestamp asked per call (verdict `.valid`,
the authority's name and certificate, one query with an 8-octet nonce); a long-term fill from the
settings (`ocspResponses` > 0, the OCSP POST, `validate` with `.earliestTimestamp`: `.valid`,
revocation `.good`, `longTerm.enabled`, nothing missing), the settings patch and
`addValidationInfo` refusing `invalid-input` without a route; `.without` sending no query. One call
per other surface: the standard commands named out of order and joined as upstream, with
`document:download` reaching the platform; `ToolPermission` deciding `canUseTool`;
`hasCreationAwaitingClick`; `StampImportOptions.id`; the new settings fields.

## Results

`scripts/cu-c9/swift-tests.sh`, the three runs the publish script makes: iOS simulator `iPhone 17`
(`8D9A9044-5872-498A-80F7-D88D9C2C4BD3`, iOS 27.2) in place, macOS through a private consumer
manifest. Xcode 27.2. Head runs had the routes helper's address set.

| Check | `cf7fdbe7a` (published `apple-engine-9c1ce4a8ff1cd488`) | Head (`6e39c46c6`, local framework) |
|---|---|---|
| `check-swift-api-coverage.py` | exit 1, 18 errors | exit 1, the same 18 (22 at `72287b00c` before mapping) |
| iOS simulator | 506 of 506; UI 77 of 77 | 519 of 519; UI 77 of 77 |
| macOS arm64 | 506 of 506; UI 77 of 77 | 519 of 519; UI 77 of 77 |
| macOS x86_64 | 506 of 506; UI 76 of 77 (`resubmittingAfterAnEditRereadsHitsWhoseCountDidNotChange`) | 519 of 519; UI 77 of 77 |

Skipped in both columns: `theRepositoryRootManifestExportsOnlyTheFacadeAndItsUI` on macOS (the
staged copy is outside the repository). The 13 new tests are the whole difference in count. The
coverage check's 18 errors predate the catch-up: 17 unaccounted members such as `wheel_by` and
`duplicate`, and the stale `remeasure_page`.

Two head attempts failed before the final one, both in the tests, not the framework:
`DisabledPluginTests.everyPluginInViewerConfigHasARow` counted `standardCommands` as a plugin
(CU-C9-D5), and the nonce check read the routes helper's whole log, which a second run had grown.
Both were fixed in `6e39c46c6`; their logs are `head-try1/` and `head-try2/`.

## Not run

The publish script (`publish-engine-payload.mjs`), by the brief: CU-C10 publishes. The
`EmbedPDFBindingsSmokeTests` target, which exists only in `EMBEDPDF_LOCAL_ENGINE=1` mode; the three
runs above use the binary framework, as the publish does. The `cf7fdbe7a` x86_64 UI failure was not
rerun.

## Evidence

Logs in `tmp/cu1118/logs/CU-C9/` of the host checkout: `slices.log`, `build-xcframework.log`,
`package.log`, `coverage-{start-72287b00c,base,head}.log`, `head/` and `base/` (each run's
`xcodebuild` log and `status.txt`), `head-try1/`, `head-try2/`, `pnpm-install.log`,
`core-signature-build.log`. Scripts: `scripts/cu-c9/slices.mjs`, `scripts/cu-c9/swift-tests.sh`.
`scripts/cu-c7/index-check.sh`: 92 headings, 0 missing.

## Cleanup

Host free space (`df -g /System/Volumes/Data`) read 429 GB at the start and is in the implementer
report for the end, after the worktrees go; `/workspaces` was not touched (12 GB). The host
worktrees `.claude/worktrees/cu1118-swift` (4.1 GB with its `target`, `platforms/apple/.build`,
both xcframeworks and `node_modules`) and `.claude/worktrees/cu1118-swift-base` (308 MB) are removed
after the branch is committed; the test runs' derived data lived in temporary directories the run
script deletes. Kept for CU-C10: the fork tree `perf-compare/.work/fork/af480d0ce…/` (4.6 GB
apparent, mostly APFS clones of the `17d8073d4` tree), whose four stamps let `buildFork` skip the
slice builds when the publish runs.
