# CU-C9 decisions

## CU-C9-D1. The Apple slices build in a fork tree seeded from the cached `17d8073d4` tree, without a dependency sync

Decision: `perf-compare/.work/fork/af480d0ce…/` was made by cloning (`cp -cR`, APFS) the cached
`17d8073d4` fork tree and its gclient files, deleting its `out/`, and checking out `af480d0ce` in
it. `buildFork` then built the four targets with `PDF_RUNTIME_SYNC=skip`, which it picks itself for
a native target when the tree's clang runs on the host and the stamp's `target_os_list` covers
`ios` and `mac`.
Reason: the only `DEPS` changes from `17d8073d4` to `af480d0ce` are `depot_tools_revision` and
`tools_memory_revision`. Neither is in the PDFium build graph: `buildFork` puts its own
`depot_tools` on `PATH`, and `tools/memory` is sanitizer tooling. A fresh tree would need a full
gclient sync, where this one built each slice in 56 to 77 seconds.
Cost: the tree's `third_party/depot_tools` and `tools/memory` stay at the `17d8073d4` revisions,
and its `deps-sync.stamp` names the old `DEPS` hash, so a later `PDF_RUNTIME_SYNC=auto` resyncs.
Reverse: delete the tree and let `buildFork` clone and sync it (`PDF_RUNTIME_SYNC=always`).

## CU-C9-D2. `URLSessionSignatureTransport` reads each reply through a per-task delegate

Decision: `post` builds one `URLRequest` (POST, `content-type` then the request's headers, the
body), starts a data task on the caller's session (default `.shared`) with an `Exchange` as the
task's own `URLSessionDataDelegate`, and schedules one deadline at `timeoutMs`. The delegate sees
the status before the body, refuses a 2xx `Content-Length` over `maxReplyBytes`, and counts bytes
as they arrive. The first of reply, deadline or failure is the answer; a lock keeps it to one.
Reason: the status has to be known before the body ends, for a non-2xx reply whose body stalls or
runs long (CU-C8b-D1), and a size limit needs the bytes as they come. A completion-handler task
gives neither.
Cost: the deadline is not cancelled after an answer; it fires later and does nothing, keeping the
exchange alive until `timeoutMs` (10 s by default). A session whose own delegate implements the data
methods doesn't see this transport's tasks' data.
Reverse: a session-level delegate keyed by task, with a cancellable timer.

## CU-C9-D3. A body shorter than its `Content-Length` counts as broken off

Decision: when a task ends without an error but the body is shorter than the reply's
`Content-Length`, the exchange treats it as `networkConnectionLost`: a 2xx is `failed`, a non-2xx is
its status with no body. The check applies only to a reply with no content coding (or `identity`).
Reason: in `maxReplyBytesIsEnforced`, a 503 that declared 100 bytes, sent 10 and closed the
connection ended its `URLSession` task with no error, and the first run answered `replied 503 10`.
Cost: a reply with a content coding that breaks off is caught only if `URLSession` reports it.
Reverse: drop the check, and a short 2xx body reaches the client as a reply.

## CU-C9-D4. The route tests reuse CU-C8c's helper and run only when its address is set

Decision: `SignatureTransportTests`' three route tests read `EMBEDPDF_SIGNATURE_ROUTES`
(`TEST_RUNNER_EMBEDPDF_SIGNATURE_ROUTES` through `xcodebuild`), the address of
`platforms/android/tests/signature-routes.mjs`, started by hand before the run. Without it they are
skipped. The transport outcome tests use a loopback `NWListener` server in the test target and
always run.
Reason: a Swift test on the simulator cannot start Node, and the helper already serves upstream's
`timestampRoute` and `revocationRoute` with a nonce-echoing authority (CU-C8c-D4). The simulator
reaches `127.0.0.1` on the host, with no App Transport Security exception needed in the test host.
Cost: a plain `xcodebuild test`, and the publish script's three runs, skip the granted timestamp and
the long-term fill. The helper needs `packages/core/signature/dist` built.
Reverse: start the helper from the publish script and pass its address.

## CU-C9-D5. The Swift surface is aliases and thin members, with no closure adapters

Decision: every new uniffi type gets a same-name `typealias` beside its plugin's others
(`SignatureRequestedTime` next to `SignatureValidationTime`), and every new handle method a member
on its capability or `PDFViewer`: `addValidationInfo(_:)`, `canAddValidationInfo()`,
`setSignatureTransport(_:)`, `setCommandsPlatform(_:)`, `registerTool(_:replace:permission:)`,
`canUseTool(_:)`, `hasCreationAwaitingClick()`, and `ViewerConfigBuilder.standardCommands(_:)`.
`ToolPermission` and `StandardCommandsPlatform` are taken as protocols.
Reason: the wrapper's rule is a same-name alias per generated type the facade uses, and
`CommandHandler` is the one closure adapter, for a protocol with six hooks; these have one and three
methods.
Cost: an app writes a small class for a tool permission or the commands platform.
`ViewerConfig.standardCommands` is not a plugin, so `DisabledPluginTests` leaves it out of its plugin
rows beside `scope`.
Reverse: add closure-taking initializers later; nothing here would change.

## CU-C9-D6. The start column is `cf7fdbe7a` against its own published engine

Decision: the `cf7fdbe7a` runs use the package's default download mode in a detached worktree of
that commit, with the fixtures copied in. `apple-engine-hash.sh` gives `9c1ce4a8ff1cd488` for that
tree, the release its `Package.swift` pins. The head runs use the framework this job packaged, with
`EMBEDPDF_PREBUILT_ENGINE=local`.
Reason: the pin is current at `cf7fdbe7a`, so the downloaded framework is that commit's engine
without a second 16-minute build.
Cost: the two columns get their engine two ways (download against a local zip). Both are built by
the same scripts.
Reverse: build `cf7fdbe7a`'s framework locally and run it with `EMBEDPDF_PREBUILT_ENGINE=local`.
