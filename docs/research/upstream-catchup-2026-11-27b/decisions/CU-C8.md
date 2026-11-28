# CU-C8 decisions

Decisions made while exposing anyone fields (#1163) and picture buttons (#1165) in Swift, on
`research/catchup-2026-11-27b-swift` from `064026d17`, on the Mac host.

## CU-C8-D1. The engine framework is built from 2026-11-27's `9472117ed` slices

Decision: `build-xcframework.sh` ran with `LIB_ROOT` at
`perf-compare/.work/fork/9472117edbc7d9d980888c2418573081a76f532f/runtime-src/out/embedpdf-runtime-staging`
of the main checkout (2026-11-27 CU-C9's four slices, CU-C9-D1 of that run), `EMBEDPDF_NM` at Rust
1.100.0's `llvm-nm` and `CARGO_BUILD_JOBS=8`; no slice was rebuilt.
Reason: the runtime pin is still `9472117ed` (`git submodule status` at `064026d17`), so the fork
slices are the same inputs; only the Rust core and its uniffi records changed.
Cost: the slices are a local build, not a release's.
Reverse: rebuild them with `docs/research/upstream-catchup-2026-11-27/scripts/cu-c9/slices.sh`.

## CU-C8-D2. `setButtonImage(_:image:)` is cancellable and discardable, with no coverage row

Decision: `FormCapability.setButtonImage(_ field: FormFieldRef, image: Data?)` returns
`FormFieldUpdateResult`, goes through `bridgedCancellable` like the other edits, and is
`@discardableResult`. `FormWidgetLayout` and `FormFieldUpdateResult` are aliases with the Kotlin
docs; `Form.swift` gains `public import Foundation` for `Data`. `swift-api-coverage.json` is
unchanged. Besides Kotlin's three cases, `PictureButtonsTests` has
`setButtonImageCancelsWhenTheTaskIsCancelled`, the `<member>CancelsWhenTheTaskIsCancelled` check
`FormTests` writes for each cancellable form edit.
Reason: CU-C7-D2 put `set_button_image` on `FormHandle` alone, and the coverage script inventories
`Core` and capability methods, so it has no row to map. `@discardableResult` matches `updateWidget`
and `deleteWidget`; a caller that only puts a picture in has no use for the rows.
Cost: only the tests guard the call; a rename in the FFI is caught at compile time, not by the
coverage check.
Reverse: drop the attribute or the cancel test.

## CU-C8-D3. The start column is 2026-11-27 CU-C9's head, not a new run at `0afd87825`

Decision: the Swift test counts at the start are 2026-11-27 CU-C9's head row (`5a7abb9fe`,
local framework). Only the coverage check was rerun at `0afd87825`, in a temporary detached
worktree.
Reason: `git diff --stat 5a7abb9fe 0afd87825 -- platforms/apple` changes only
`platforms/apple/EmbedPDF/Package.swift` (the release pin, two lines), and `0afd87825` to
`064026d17` changes nothing under `platforms/apple`. The Swift sources and tests the counts come
from are the same.
Cost: the start column is a day old and ran on another simulator (`8D9A9044`, iOS 27.2).
Reverse: run `scripts/cu-c8/swift-tests.sh` at `0afd87825` in download mode.

## CU-C8-D4. The signature route tests ran, through a bridge to the container

Decision: `platforms/android/tests/signature-routes.mjs` ran in `embed-pdf-catchup1127b`, in
`/workspaces/wt/CU-C2` (it has `packages/core/signature/dist` built and `node_modules`), and
`scripts/cu-c8/routes-bridge.py` gave the Mac `http://127.0.0.1:64257`, which passes each
connection to the helper's port through `docker exec -i … python3`. All three runs got it as
`TEST_RUNNER_EMBEDPDF_SIGNATURE_ROUTES`, so `aGrantedTimestampThroughTheTimestampRoute`,
`aLongTermFillThroughTheRevocationRoute` and `aCallWithoutAnAuthoritySendsNoQuery` ran and passed;
the helper's `/log` after the runs read 6 timestamp queries and 3 OCSP posts.
Reason: the brief asks for them, the catch-up changed signatures (CU-C4, CU-C5), and Node may run in
the container, not on the Mac. Docker Desktop does not route the Mac to a container's loopback.
Cost: the CU-C2 checkout's copy of the helper ran (no file under `packages/` differs, CU-C7-D11),
and the start column skipped these three, so they are not a like-for-like row.
Reverse: run without a routes address; the tests skip.

## CU-C8-D5. A simulator of the job's own

Decision: the iOS run used `CU-C8 iPhone 17` (`F0B84285-AE00-4AC2-A7B6-6093F15E3016`, iPhone 17,
iOS 27.2), created for this job and deleted at cleanup.
Reason: `8D9A9044`, the simulator 2026-11-27 CU-C9 used, was booted by another session running
`xcodebuild test-without-building`; the other non-broken "iPhone 17" is iOS 26.5.
Cost: a cold simulator, so the first launch is slower.
Reverse: set `EMBEDPDF_TEST_DESTINATION` to any iOS 27.2 simulator.
