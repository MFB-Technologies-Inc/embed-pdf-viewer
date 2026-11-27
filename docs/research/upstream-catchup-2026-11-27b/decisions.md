# Decisions: upstream catch-up 2026-11-27b

Every decision that resolved an ambiguity in this catch-up. Manager decisions are `D-n` (the plan's
"Decisions", accepted by the user on 2026-11-27); a job's decisions are `CU-Cn-Dn`, with the full
text (reason, cost, how to reverse) in [`decisions/`](decisions/). Each line here states the
decision and, where it has one, its cost, and links the job's record for the rest.

Targets: upstream viewer `cda9e82a4984cd7edb1a69a8fe32b1d7e11693b8`, upstream runtime `7f399f838`
(unchanged from the 2026-11-27 run). Starting points: plugins `0afd87825`, runtime pin `9472117ed`,
which already merges `7f399f838`.

## NEW DIVERGENCE

None recorded yet.

The divergences accepted in earlier catch-ups stand as before
(`docs/research/upstream-catchup-2026-11-27/decisions.md`,
`docs/research/upstream-catchup-2026-11-18/decisions.md`,
`docs/research/upstream-catchup-2026-11-12/decisions.md`,
`docs/research/rust-plugins-branch-review.md`).

## Scope, authority and branches

- D-1. The deprecated attribution fields follow upstream: the Rust core keeps `filled_by`,
  `filled_by_name` and `filled_at`, always `None`, and the wrappers mark their properties
  deprecated. Cost: three dead properties per platform and deprecation warnings in our own tests
  until upstream's 4.0.
- D-2. `completeFormBundle` is ported in CU-C4, with the `forAnyone`, `hasImage`, `layout` and
  `purpose` defaults. Cost: about two hours in CU-C4; 2026-11-27 CU-C4-D9's divergence closes.
- D-3. A Rust test in `epdf-engine` checks each `signature-compat/v4/` fixture against
  `policy-expectations.json`. Cost: about half a day in CU-C4.
- D-4. This run starts beside the 2026-11-27 run on `-b` names and merges plugins' post-release tip
  in CU-C6. Cost: two catch-up containers on the disk for a few hours.

## CU-C2: setup, merge, start state ([record](decisions/CU-C2.md))

- CU-C2-D1. `pnpm-lock.yaml` stays as merged; upstream changed no dependency. Cost: none.
- CU-C2-D2. `sync-upstream.sh` made no runtime merge; the fork already has `9472117ed`. Cost: none.
- CU-C2-D3. The wasm32-eh `9472117ed` archive is the host copy of 2026-11-27's local build. Cost:
  a local build, not a published one.
- CU-C2-D4. `embedpdf-dev:latest` is reused and the clang shim stays on `PATH`. Cost: the
  generator bug behind the shim is still not fixed.
- CU-C2-D5. The `0afd87825` baseline is measured fresh. Cost: one suite run of container time.
- CU-C2-D6. FreeType's source is fetched into each `runtime-src` for the `core-fonts` test. Cost:
  one more container-only setup step.

## CU-C3: vectors and the work order ([record](decisions/CU-C3.md))

- CU-C3-D1. The plugin generator's 9 type errors stay and the plugin vectors come from it as it is;
  the three-line fixture fix needs a ruling. Cost: `typecheck` stays red, and fixture-fed plugin
  vectors regenerate once more when the fix lands.
- CU-C3-D2. CU-C4 reads all three new row members, `layout` and `hasImage` included. Cost: about
  an hour moves from CU-C5 to CU-C4.
- CU-C3-D3. The signature mark's move to the shared drawing (`7d5691a5a`) is CU-C5's. Cost: two
  mark cases differ on object numbers until CU-C5.
- CU-C3-D4. The evidence archives are re-pinned in CU-C6, not here. Cost: 17 extra evidence
  failures through CU-C4 and CU-C5.
- CU-C3-D5. D-1 leaves nothing to deprecate in Swift, Kotlin or .NET: no wrapper types `filledBy`.
  Cost: a caller reading it out of `fields_json` gets `null` with no compiler warning.
