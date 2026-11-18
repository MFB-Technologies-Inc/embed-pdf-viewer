# CU-C3 decisions

Decisions made while merging the impl catch-up into plugins, regenerating the vectors and measuring
the port on `research/catchup-2026-11-18-plugins`. Each names the commit that carries it; "the docs
commit" is the one that adds this file.

## CU-C3-D1. Start from CU-C1's record commit, then merge the impl catch-up

Decision: the job branch starts at `cf7fdbe7a` and fast-forwards to `af0c51003`
(`research/catchup-2026-11-18-c1`) before the merge `44dcf70da` of `research/catchup-2026-11-18-impl`
(`474599496`).
Reason: CU-C2-D1 left CU-C1's record for this job, since its branch sits on plugins. `af0c51003`
adds only `docs/research/upstream-catchup-2026-11-18/` files, so the fast-forward changes nothing
the checks read, and the merge-tree conflicts are the same from either start.
Cost: the branch carries CU-C1's report and scripts, which a reader of plugins may not expect.
Reverse: rebuild the branch from `cf7fdbe7a` without `af0c51003`.

## CU-C3-D2. Lockfile: the impl catch-up's, re-resolved for plugins' importers

Decision: `pnpm-lock.yaml` took impl's side, then `pnpm install --no-frozen-lockfile` (`44dcf70da`).
Reason: impl's lockfile is upstream's plus the fork's tooling (CU-C2-D3). After the install it
differs from impl's only in the four importers plugins adds (`examples/rust-kernel-eval`,
`packages/core-wasm`, `packages/rust-viewer-bundle`, `tooling/plugin-conformance`), their
packages, and one shared snapshot whose `minimatch` dedupes from 10.2.1 to 10.2.6 next to them
(`ignore-walk@8.0.0`; `CU-C3-lockfile-diff.log`). `pnpm install --frozen-lockfile` then passes. Playwright stays
at the 1.65.1 CU-C2 resolved.
Cost: the same Playwright drift CU-C2 reported.
Reverse: `git checkout cf7fdbe7a -- pnpm-lock.yaml` and re-resolve.

## CU-C3-D3. `pages/mutate.json` conflict: plugins' copy, then regenerate

Decision: the conflict resolved to plugins' copy in `44dcf70da`; `922c08d2a` regenerated it.
Reason: both sides were generator output (plugins' added flatten and export cases, impl's moved
`policyVersion` to 5). Regenerating on the merged tree gives both, and the regenerated file has
plugins' cases with `policyVersion` 5.
Cost: none.
Reverse: not applicable.

## CU-C3-D4. Follow upstream in the generators, including one case's answers and covers

Decision: `3b37c715f` sets the wire generator's `EXPECTED_KINDS` to 96, adds `getValidationInfo` and
`addValidationInfo` to the fake engine's signatures members, answers `getValidationInfo` with empty
lists in the signature-core and signature plugin documents, and rewrites
`ink-cancel-inside-the-grouping-window`: it now answers the ink create and its covers text says the
waiting stroke is kept.
Reason: upstream added the two kinds and the two members (`67ed292a6`, `656c2fc42`);
`validateSignatures` reads the security store before it judges (`readValidationPool`), so every
validating case asked an unanswered `getValidationInfo`; and `d0601e65e` (#1096) keeps a pen drawing
that waits for its next stroke, so the square down in that case now creates the ink annotation the
case used to show being dropped. The generator stopped with "unanswered engine calls" on each.
Cost: the case keeps its name, which now describes behaviour upstream removed; a rename would have
broken the accounting archive's mapping.
Reverse: revert `3b37c715f` and `98a477901`.

## CU-C3-D5. Re-baseline the evidence archives by re-pin, and change one witness

Decision: `48568034c` re-pins source rows that named a path's last owner at `cf7fdbe7a`
(`srcpins.py`), `currentOwners` rows only for paths upstream changed in `ed064e0c8..c4e744345`
(`owners.py`), moves the chained integrity and aggregate pins (`chainpins.py`), re-pins the
`task-12b2b1b-ii` accounting row of `ink-cancel-inside-the-grouping-window` (`fixrow.mjs`), and
changes the `annotation-live-ii` witness for that case to the kept stroke.
Reason: the brief asks for a re-baseline. `owners.py` without the range filter also listed four
`task-12b2c8` rows that were stale before this merge (owners `b3f1c447e`, `a390171cc`,
`c7f8d75ff`), and those aren't this job's. The witness changed because the upstream commit changed
the outcome, not the pin.
Cost: the evidence for that one case now asserts current behaviour rather than the behaviour its
archive was cut from.
Reverse: revert `48568034c`.

## CU-C3-D6. List coverage gaps, add no cases

Decision: no generator cases for upstream code no vector reaches; `results/CU-C3-coverage-gaps.md`
lists them with the job that adds them.
Reason: the brief, and CU-C3-D7 of 2026-11-12. Most of the gap is `core/signature` (2,148 lines), and
the D-1 transport decides how those cases are driven.
Cost: `coverage:check` passes against a `coverage.json` that records the gaps.
Reverse: not applicable.

## CU-C3-D7. Trust reasons that are pkijs 4 `TypeError` text go to CU-C4 as they are

Decision: the four `core/signature-trust.json` cases whose reason is "Cannot read properties of
undefined (reading …)" are left as the generator wrote them, and CU-C4 decides whether the Rust
port reproduces the text or records an outcome exception.
Reason: it is what the shipping TypeScript returns under pkijs 4 (`a841027e9`, #1092). Changing the
vector would hand-edit the oracle.
Cost: until CU-C4 decides, the trust replay can't be fully green without copying a JavaScript
engine's error text.
Reverse: not applicable.

## CU-C3-D8. Rebuild the wasm32 payload after the runtime moved

Decision: after the after-suite, the local `npm/wasm32/lib` payload and `build/libpdfium/wasm32` were
deleted and rebuilt with `EMBEDPDF_RUST_CORE=0 pnpm --filter @embedpdf/engine-runtime build:target
wasm32`, which fetched the `f2ea75972` library; the TS tests, both engine regenerations and the
engine checks were run again on it.
Reason: `fetch-payload.sh` leaves a locally built payload untouched, so prep kept the one built at
baseline from `aa59ce554`. Under it 3 TS tasks failed with "Cannot call unknown function
EPDFDss_GetCount". On the rebuilt payload the TS tasks are 98 of 98, and the engine vectors come out
identical to `922c08d2a`.
Cost: prep alone is not enough when `engine-runtime-build.json` moves; later jobs should delete the
payload first.
Reverse: not applicable.

## CU-C3-D9. `replay:check` is in the check set; `rust-coverage:check` is not

Decision: the plugin checks run `replay:check` before and after; `rust-coverage:check` was not run.
Reason: the conformance gates are part of every check (brief). `replay:check` reuses the cargo build.
`rust-coverage:check` needs `cargo-llvm-cov`, which the container doesn't have, and an instrumented
build of every reported crate; it is a record that changes with the ports, so CU-C7 is the job to
regenerate it.
Cost: `vectors/rust-coverage.json` is not checked here.
Reverse: install `cargo-llvm-cov` and run it before and after.

## CU-C3-D10. Route the plugin side of #1087 and #1088 to CU-C6, and `canUse` to CU-C5

Decision: the work order gives the `plugin/signature` and `plugin/form` parts of #1087 and #1088 to
CU-C6 with #1102's, and the tool `canUse` / `canUseTool` changes (#1091) to CU-C5, though they fail
in `epdf-plugin-annotation` and `epdf-plugin-form` too.
Reason: the plan names only #1102's plugin parts for CU-C6, but the same files carry #1087's and
#1088's settings, `addValidationInfo` and `SignFieldResult`, and they depend on CU-C4's
`signature-core`. `canUse` is part of #1091's command move (`d6325f5ba`).
Cost: CU-C6 grows by the plugin half of signing; CU-C5 touches the annotation and form crates.
Reverse: move the items back in the work order.

## CU-C3-D11. Add the native host's engine-call table to the work order (review fix)

Decision: CU-C4 gets item 12, the native host's half of `LocalDocumentSignaturesService.ts`:
`epdf-viewer-host`'s `translate.rs` rows for `getValidationInfo` and `addValidationInfo`, the gates
and the conditional `signatures.validationInfoAdded` event in `epdf-engine`'s host, and the
no-field `prepare` gate and event field. `epdf-kernel`'s `changes_document` stays as it is.
Reason: review found that the first work order covered the worker side (`WorkerHost.ts`) and the
plugins but not this layer, so a ported `readValidationPool` would pass replay against the fake
engine and then call an untranslated member on every native platform. The upstream `kernel.ts`
`changesDocument` is unchanged in `ed064e0c8..c4e744345` and the Rust copy excludes the same four
events, so the new event already counts as a change. A sweep of the rest of the range found no other
host-side gap: `WorkerHost.ts` and `DocumentSession.ts` are items 2 and 3; the cloud wire paths,
resources, cached reads and tokens (`packages/engine/core/src/wire/{paths,resources,cachedReads,
tokens,tokenSchemas}.ts`) have no Rust counterpart, since no crate ports the cloud REST surface;
`epdf-node`, `epdf-web`, `epdf-ffi`, `epdf-capi` and `epdf-uniffi` carry no per-kind signature
table (`grep` finds none) and pass kinds through to `epdf-engine`'s host; `core/main`'s
`commands.ts` uses `canDownload`, `download`, `canPrint` and `print`, which `epdf-kernel` already
has (`kernel.rs`), and is CU-C5 item 2.
Cost: CU-C4 grows by one layer, with its own tests in `epdf-viewer-host`.
Reverse: move item 12 to CU-C6, which first calls these members.
