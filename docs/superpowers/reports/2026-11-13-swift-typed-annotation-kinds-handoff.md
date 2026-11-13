# Swift typed annotation kinds handoff, 2026-11-13

## Resume here

Claude Code session: `1bd64fdb-6067-4821-850a-f3e05d19c495`
(resume with `claude --resume 1bd64fdb-6067-4821-850a-f3e05d19c495` from
`/Users/andrew/repos/embed-pdf-viewer`). Cloud session id:
`session_01R27pzXNAq9SEtTjdWdw6Js`.

The work is done and committed on `feat/apple-typed-annotation-kinds`, branched
from `research/multi-platform-rust-core-impl-plugins` at `7990560d6`. It is not
merged into the plugins branch and not pushed. Both need the user's go-ahead.

## What changed

Before this branch the Swift package had typed drafts only for ink, highlight
and note, and no `blendMode` anywhere. Every other subtype went through the
JSON-string overloads. The typed layer had been scoped to the subtypes
`AnnotationInfo` reads, and `blendMode` was left out under the same rule.

Now every subtype the engine creates from a field bag has a typed draft and
patch, in `platforms/apple/EmbedPDF/Sources/EmbedPDF/Plugins/`:

- `AnnotationDraft.swift`: ink, highlight and note gain `blendMode` as the last,
  defaulted init parameter, so existing callers compile. Shared vocabulary lives
  here too (`AnnotationBlendMode`, line endings, alignment, intents, caption and
  callout types, link targets).
- `AnnotationDraft+Markup.swift`: underline, squiggly, strikeout, redaction.
- `AnnotationDraft+Shapes.swift`: square, circle, line, polygon, polyline.
- `AnnotationDraft+Boxes.swift`: free text, caret, link; patch-only stamp and
  file attachment, since their creates need bytes.
- `AnnotationInfo.swift` and `AnnotationInfo+Kinds.swift`: `blendMode` on
  `AnnotationInfo` and a typed `Details` case for every subtype but popup and
  widget, which stay `.untyped`.

The field spellings come from the engine's field bag rules in
`crates/epdf-engine/src/features/annotation/write/prepare.rs` and the read
records in `read/kinds.rs`.

## Deliberate gaps

`richText`, `measure`, the `/F` flags, `nm`, `subject`, `reply` and `groupId`
stay on the JSON-string overloads.

Link and stamp patches have no `blendMode`. The engine writes `/BM` only while
generating an appearance (`write/update.rs:341`, `write/create.rs:202`), and it
generates none for either, so the value would be accepted and dropped. A stamp
round-trip test caught this.

## Costs

New `AnnotationInfo.Details` cases break any outside exhaustive `switch` over
it. Every typed member also has to track the engine schema;
`AnnotationKindDraftTests` writes each member through the engine and fails when
one does not come back.

## Verification

`EMBEDPDF_LOCAL_ENGINE=1 xcodebuild test -scheme EmbedPDF-Package -destination
'platform=iOS Simulator,id=5FA32A5D-6186-4C16-BE25-C87829DD1BFA'` passed: 522
tests in 52 suites, 66 tests in 14 suites, and 8 XCTest tests. The macOS build
succeeded with no new warnings. A single test runs with
`-only-testing:EmbedPDFTests/BaseSuite/<Suite>/<test>()`; the trailing `()` is
required.

## Open items

1. Merge `feat/apple-typed-annotation-kinds` into the plugins branch, once the
   user approves.
2. The main checkout shows `packages/engine/runtime/runtime-src` at `3c6e02297`
   while the plugins branch pins `17d8073d4`. That drift is not from this work
   and was left uncommitted.
