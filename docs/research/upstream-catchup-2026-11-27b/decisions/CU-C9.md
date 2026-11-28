# CU-C9 decisions

Decisions made while merging plugins' moved tip and preparing the release, on
`research/catchup-2026-11-27b-swift` from `6bef14419`, on the Mac host and in container
`embed-pdf-catchup1127b`.

## CU-C9-D1. Plugins `7a4673b42` is merged as fetched

Decision: `git fetch fork research/multi-platform-rust-core-impl-plugins` returned `7a4673b42`, the
tip the controller named, and `9d66eb489` merges it with no conflict. Nothing under `packages/**`
came with it besides `packages/rust-viewer-bundle/package.json` (`0.0.0-preview.8`).
Reason: the brief's step 1; the tip had not moved again.
Cost: none.
Reverse: `git reset --hard 6bef14419` before anything is pushed.

## CU-C9-D2. The container rows ran at the merge, `9d66eb489`

Decision: cargo test, engine `:check`, the four plugin checks, `replay:check`, the evidence tests
and `rust-coverage:check` ran at `9d66eb489` in `/workspaces/wt/CU-C2`, switched to
`research/catchup-2026-11-27b-swift` (bundle `CU-C9a`). They were not rerun after the commits that
follow.
Reason: `git diff --stat 9d66eb489 HEAD` touches only doc comments in `Form.kt`, `Form.swift` and
`Form.cs`, the two pin files, `packages/rust-viewer-bundle/package.json` and Markdown. None is an
input to a Rust, vector or evidence row. The worktree kept its `node_modules`, built `dist` and
libpdfium, and `git diff --stat 0c6097639 9d66eb489 -- packages` is the `package.json` line alone.
Cost: the table's commit is not the branch tip.
Reverse: rerun `scripts/cu-c9/run.sh` at the tip.

## CU-C9-D3. The Swift runs use a framework rebuilt from the same `9472117ed` slices

Decision: `build-xcframework.sh` rebuilt the Rust core at `9d66eb489` with `LIB_ROOT` at the main
checkout's `perf-compare/.work/fork/9472117edbc7d9d980888c2418573081a76f532f/runtime-src/out/embedpdf-runtime-staging`,
as CU-C8-D1 did, and `scripts/cu-c8/swift-tests.sh` ran with `EMBEDPDF_PREBUILT_ENGINE=local`.
The signature route tests ran without a routes address, so they skip.
Reason: both sides changed the engine (`6b71b4243` on plugins, CU-C4 and CU-C5 here) and the
runtime pin did not move. The route tests exercise neither change; CU-C8-D4 ran them through the
bridge at `663cccac6`.
Cost: a local framework, not the train's; the three route tests are not in this job's counts.
Reverse: run `swift-tests.sh` with a routes address from `routes-bridge.py`.

## CU-C9-D4. The .NET and Android pins name packages the train will publish

Decision: `37996bdbb` pins `EmbedPDF.Runtime` `0.0.0-engine.7dd0c2b9e60f92a1` and `513fcb0ce` pins
`android-engine-1a68bff677299ac0`, the hashes the scripts give for the tree. Both checks exit 0.
The root `Package.swift` and `platforms/apple/EmbedPDF/Package.swift` stay at
`apple-engine-ca2421677d81b1d9`; the train's `pin` job moves them.
Reason: the 2026-11-27 run's CU-C10-D2, repeated for its last run in `05c9ede7c` and `54fa68a14`.
Exit 0 means the pin equals the tree's hash, not that the package exists.
Cost: from the push until the train's publish jobs finish, the plugins tip names an
`EmbedPDF.Runtime` and an `embedpdf-bindings` that don't exist yet.
Reverse: revert `37996bdbb` and `513fcb0ce`.

## CU-C9-D5. The Swift notes name `apple-engine-44a23eb0573339c7` ahead of the train

Decision: `docs/releases/0.0.0-swift.12.md` names the engine `platforms/apple/apple-engine-hash.sh`
gives for the tree, as `0.0.0-swift.11.md` named `ca2421677d81b1d9`.
Reason: the hash is deterministic over its inputs, and the notes publish with the tag the pin commit
makes.
Cost: any later change to an Apple hash input before dispatch (a Rust source, `build-xcframework.sh`)
makes the note wrong; recheck it if plugins moves again.
Reverse: edit the hash in the note.

## CU-C9-D6. `dotnet-engine.md` stays as it is

Decision: `docs/releases/dotnet-engine.md` is not edited.
Reason: `epdf-capi` has no change (CU-C7-D7) and nothing the file says moved; the same reasoning as
the 2026-11-27 run's CU-C10-D3.
Cost: the `dotnet-engine-0.0.0-local-<sha12>` release doesn't describe the form changes, which the
C# API does not reach through the C ABI anyway.
Reverse: edit the file before dispatch.

## CU-C9-D7. Each platform's notes list the breaks that platform has

Decision: the C# notes call three positional records breaking (`FormField`, `FormWidgetRecord`,
`FormFieldDetail.Pushbutton`) and the inputs' new `init` properties non-breaking, as
`PublicAPI.Unshipped.txt` shows. The Swift and Kotlin notes call all five generated records
breaking, since uniffi records have a memberwise initializer or a primary constructor. Each note
carries the `filledBy`, `filledByName`, `filledAt` line (D-1, CU-C3-D5) and the `setButtonImage`
undo caveat.
Reason: the brief's amendments; the type shapes differ per platform.
Cost: four notes that read alike but are not interchangeable.
Reverse: edit the notes.

## CU-C9-D8. `rust-coverage.json` is regenerated in the container and committed on the host

Decision: `rust-coverage:check` at `9d66eb489` exited 1 (stale); `rustcov.sh … write` regenerated
the record in `/workspaces/wt/CU-C2`, `rustcov.sh … check` then exited 0, and the file was copied to
the host with `docker cp` and committed as `37c92d414` (same sha256, `4d4b5f0b`), not brought back
as a bundle.
Reason: the host branch had moved past `9d66eb489` by then, so a container commit would not
fast-forward; the record is one generated file. The only change is
`crates/epdf-engine/src/features/actions/reader.rs`, `0/200` to `0/238` lines, which is plugins'
`6b71b4243`: the replay reaches no more and no fewer lines (67,236).
Cost: the commit was not made where the file was generated.
Reverse: `git revert 37c92d414`, and `rust-coverage:check` is stale again.

## CU-C9-D9. The iOS run's one failure is the known attachment time limit

Decision: the first iOS run failed `AttachmentModelTests.importDownloadExportAndBothHomesUseOwnedBytes`
on its 60-second limit (`platforms/apple/EmbedPDF/Tests/EmbedPDFUITests/AttachmentModelTests.swift:65`);
the whole iOS run, rerun once, passed 82 of 82, and both macOS runs passed it the first time.
Reason: the 2026-11-27 run's CU-C9 saw the same test hit the same limit on a loaded Mac and pass
alone. Nothing in this catch-up or the merge touches attachments or `EmbedPDFModels`.
Cost: a flaky UI test stays flaky; the train's hosted simulator may hit it too ("Re-run failed
jobs").
Reverse: none; raise the test's time limit if it recurs in the train.
