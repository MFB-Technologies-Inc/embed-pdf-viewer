# Phase 3 Wave E: Redaction, Measurement, Stamp and Signature Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Typed C# capabilities for redaction, measurement, stamp and signature, with their .NET platform services (`FileStampLibraryStore`, `X509CertificateSigner`, `TrustAnchors`, `HttpClientSignatureTransport`), so every wave E row of `platforms/dotnet/csharp-api-coverage.json` (142: measurement 34, redaction 30, stamp 39, signature 39) is ported or excluded and no `pending` row is left (the Phase 3 exit).

**Architecture:** The wave A to D recipe: a public `*Capability` over the internal generated handle, hand-written public records with internal mappings in `Plugins/<Plugin>.Ffi.cs`, a `Get{Plugin}Async` accessor, streams through `PdfViewer.Stream` and `EventRelay`, foreign traits behind a public C# interface and an internal adapter that marks the kernel thread and contains every exception. The twins and mappings start from a scratch generator over the generated C# (records, unions, enums, Rust doc comments), then are edited by hand; the generator is not committed.

**Tech Stack:** .NET SDK 10.0.403 `net10.0`, xUnit 2.9.3, PublicApiAnalyzers 5.6.0, `System.Security.Cryptography` (shared framework), `System.Net.Http`.

**Spec:** `docs/superpowers/plans/2026-11-20-dotnet-parity.md` (Global Constraints, D3, D4 with `Get{Plugin}Async` and the null-argument rule, D5, D8, Phase 2 notes, the Phase 3 kernel-thread note, Phase 3 wave A to D notes), `03a-wave-a.md` to `03d-wave-d.md`, the Swift sources `platforms/apple/EmbedPDF/Sources/EmbedPDF/Plugins/{Redaction,Measurement,Stamp,Signature,KeychainSigner}.swift` and `Sources/EmbedPDF/SignatureTransport.swift`, their tests `platforms/apple/EmbedPDF/Tests/EmbedPDFTests/{Redaction,Measurement,Stamp,Signature,SignatureTransport,KeychainSigner}Tests.swift`, the measurement cases of `TypedOverloadTests.swift`, the wave E cases of `PluginSettingsTests.swift` and `IntegerSurfaceTests.swift`, and the second transport reference `platforms/android/viewer/HttpSignatureTransport.kt`.

## Global Constraints

- Dev container `embedpdf-dotnet-parity`, worktree `/workspaces/wt-p3e`, branch `research/dotnet-parity-p3e` from `a728cf2c3` (tip of `research/dotnet-parity-p3d`). Guard script before every heavy build or test run and between tasks; exit 75 or 76 stops the wave after the last complete task.
- Bindings: `CARGO_TARGET_DIR=/workspaces/embed-pdf-viewer/target platforms/dotnet/generate-bindings.sh --library /workspaces/embed-pdf-viewer/target/release/libepdf_viewer_host.so`; tests run with `-p:EpdfViewerNativeDir=/workspaces/embed-pdf-viewer/target/release/`.
- Commits by explicit path, `feat(dotnet): ...`, `test(dotnet): ...` or `chore(dotnet): ...`, no trailers. No push, no merge.
- No TypeScript or `packages/**` change, no plugin Rust change, no `ffi.rs` change (every member Swift calls is exported). `platforms/dotnet/EmbedPDF` untouched.
- Generated C# stays internal. Public integers are `int` or `long`; `WireInteger` converts. Bytes are `ReadOnlyMemory<byte>`.
- `IsAotCompatible`, `TreatWarningsAsErrors`, `Nullable`, 0 warnings. `PublicAPI.Unshipped.txt` and the coverage manifest change in the same commit as the surface.
- Null-argument rule: a method that is not `async` throws `ArgumentNullException` (or `ArgumentException` for a null element) while the call is made; an `async` method faults its task. Invalid but present arguments fault the task (conversions inside `Run`). JSON strings bound for the engine pass `Ffi.WellFormed`.
- Prose and comments name no outside product, company or person. No em dashes. Key material is never logged.

## Decisions this plan makes

- **Twins keep the generated names** (`RedactionMark`, `ScaleChangeReport`, `StampAsset`, `SignatureVerdict`, ...), `uint`/`byte` as `int`, `ulong` as `long`, arrays as `IReadOnlyList<T>`. A generated field with a default is an `init` property; one without is positional. The existing configs (`RedactionConfig`, `MeasurementConfig`, `StampConfig`, `SignatureConfig`) gain the generated config's fields, and the first three are also the `UpdateSettingsAsync` patch, as in Swift. Wave D's `PdfMeasurement`, `PdfNumberFormat`, `MeasureOrigin`, `MeasureFraction`, `MeasureLabelPosition` are reused; this wave adds their mappings to the generated measurement records.
- **Accessors:** `GetRedactionAsync(DocumentInfo)`, `GetMeasurementAsync(DocumentInfo)`, `GetStampsAsync()` (one per viewer, as Swift's `stamps()`), `GetSignaturesAsync(DocumentInfo)`.
- **Measurement readout** has three overloads: `ReadoutAsync(string annotationJson)`, `ReadoutAsync(AnnotationRef)` (writes the ref's JSON with `AnnotationPosition`'s writer) and `ReadoutAsync(AnnotationInfo)`.
- **`StampPreview`** is a record of `ReadOnlyMemory<byte>? Data` and `string? MimeType`; there is no decoded image (D6: no framework bitmap types).
- **Foreign traits:**
  - `IStampLibraryStore { IReadOnlyList<StoredStampLibrary> List(); void Put(string id, ReadOnlyMemory<byte> bytes); void Delete(string id); }`, kernel thread, synchronous, marked; a throw is the kernel's host error (`KernelCallback.RunOrFail`), so `RestoreAsync` fails with `HostError` and a failed write is dropped.
  - `ISignatureSigner { IReadOnlyList<ReadOnlyMemory<byte>> GetCertificateChain(); SignatureKeyAlgorithm Algorithm { get; } SignatureDigest Digest { get; } Task<ReadOnlyMemory<byte>> SignAsync(ReadOnlyMemory<byte> message); }`. The three synchronous members run on the kernel thread, marked; a `PdfException` from the chain keeps its name and code (`ViewerException.Kernel`), another exception is `operation-failed`. `SignAsync` runs on the thread pool (wave B's rule for completion traits), so the kernel keeps serving calls while it waits; a throw fails the signing through `FetcherAdapter.Failure`, so a cancelled prompt's `OperationCanceledException` is `operation-cancelled`.
  - `ISignatureTrust { IReadOnlyList<ReadOnlyMemory<byte>> GetAnchors(); }`, kernel thread, marked, a throw as above.
  - `ISignatureTransport { Task<SignatureTransportOutcome> PostAsync(SignatureTransportRequest request); }`, thread pool; `SignatureTransportOutcome` is a union `Replied(int Status, ReadOnlyMemory<byte> Body)`, `TimedOut`, `TooLarge`, `Failed(string Message)`. A throw or a null task answers `Failed` with the message. The adapter answers the completion exactly once.
  - `PdfViewer.SetSigner(ISignatureSigner?)`, `SetTrust(ISignatureTrust?)`, `SetSignatureTransport(ISignatureTransport?)`, like `SetCommandsPlatform`. `SignAsync(SignatureSignInput, ISignatureSigner? signer = null, ...)` names one per call.
- **Platform services:**
  - `FileStampLibraryStore(string? directory = null)`: default root `Path.Combine(Environment.GetFolderPath(SpecialFolder.LocalApplicationData), "EmbedPDF", "stamps")`; one `<escaped id>.pdf` per library, the id escaped with `Uri.EscapeDataString` and `.` escaped too, so no id names a path outside the directory; writes go to a temp file in the same directory and `File.Move(..., overwrite: true)`; deleting a missing file is not an error; `List` sorts ordinally by file name.
  - `X509CertificateSigner : ISignatureSigner` from an `X509Certificate2` with an RSA or ECDSA private key (`FromPkcs12(ReadOnlyMemory<byte>, string? password)`, `FromPkcs12File(string path, string? password)`, `FromStore(string thumbprint, StoreName, StoreLocation)`), plus intermediates. RSA answers PKCS#1 v1.5 over SHA-256; ECDSA answers IEEE P1363 `r || s`, which the engine expects (`crates/plugins/signature/src/ffi/ports.rs:56-62`). The chain (leaf first, DER) comes from `X509Chain` with `X509ChainTrustMode.CustomRootTrust` over the given certificates, falling back to the leaf plus the given certificates when the chain does not build, as Swift falls back to the leaf. `SignAsync` signs on the thread pool. The engine assembles the CMS itself, so no `System.Security.Cryptography.Pkcs` dependency is needed (a deviation from D5's wording, recorded in the notes).
  - `TrustAnchors : ISignatureTrust` from DER (`new TrustAnchors(IEnumerable<ReadOnlyMemory<byte>>)`), PEM (`FromPem(IEnumerable<string>)`, `FormatException`-free: a bad PEM throws `CryptographicException`), certificates (`FromCertificates(IEnumerable<X509Certificate2>)`) and embedded resources (`FromEmbeddedResources(Assembly, string prefix)`, every `.pem`, `.cer`, `.crt` or `.der` resource under the prefix). Swift bundles no anchors and neither does this package.
  - `HttpClientSignatureTransport(HttpClient? client = null)`: one POST, `content-type` then the request's headers (a later one wins, content headers going to the content); `timeoutMs` covers the whole exchange through a linked `CancellationTokenSource`; a 2xx reply whose `Content-Length` or body passes `maxReplyBytes` is `TooLarge`; a non-2xx reply whose body passes the limit, breaks off or is still arriving at the deadline is `Replied` with its status and no body; a 2xx body that breaks off is `Failed`; a URL that is not `http` or `https` is `Failed`. `HttpCompletionOption.ResponseHeadersRead`.
- **Exclusion:** redaction `page_list_calls` is a harness counter with no export (`crates/plugins/redaction/src/read/pending.rs:315`), like wave C's `load_starts`: `internal-resource`.

## Review Focus

1. A signer whose `SignAsync` never completes leaves the viewer serving other calls, and completing it later seals the field. Task 4 pins it (`APendingSignatureDoesNotBlockTheViewer`).
2. A signer, trust, transport or store that throws, or that calls `viewer.Dispose()`, leaves the kernel working: signing fails with the mapped error, restore fails `HostError`, the transport answers `Failed`. Tasks 3, 4 and 6 pin it.
3. A library id with `/`, `..` or `:` never writes outside the store's directory and reads back as the same id. Task 3 pins it (`AHostileIdStaysInTheDirectory`).
4. The transport answers exactly once whichever of reply, deadline or failure comes first, including a reply still arriving after the deadline. Task 6 pins it.
5. Live wave E streams and a registered store, signer, trust and transport that capture nothing leave an abandoned viewer collectable. Task 7 pins it in `LifetimeTests`.

---

### Task 1: Redaction

**Files:** create `Plugins/Redaction.cs`, `Plugins/Redaction.Types.cs`, `Plugins/Redaction.Ffi.cs`; modify `PluginConfigs.cs` (`RedactionConfig.Fill`, `OverlayText`); tests `RedactionTests.cs`, extend `IntegerSurfaceViewerTests.cs`; manifest, PublicAPI.

**Interfaces (produces):** `RedactionCapability : IDisposable` with `GetSettingsAsync`, `UpdateSettingsAsync(RedactionConfig)`, `ResetSettingsAsync`, `CanUnmarkAsync(AnnotationRef)`, `CanUpdateLabelAsync(AnnotationRef)`, `GetPendingAsync(PageRef? page = null)`, `GetPendingItemAsync(AnnotationRef)`, `GetPendingCountAsync(PageRef? page = null)` (`int`), `EstimateCollateralAsync(IEnumerable<AnnotationRef>? refs = null)`, `MarkSelectionAsync`, `MarkAreaAsync(PdfRect, PageRef)`, `MarkPageAsync(PageRef)`, `MarkMatchesAsync(SearchQuery, IEnumerable<PageRef>? pages = null)`, `UnmarkAsync(IEnumerable<AnnotationRef>)`, `ClearPendingAsync`, `UpdateLabelAsync(AnnotationRef, RedactionLabelPatch)`, `ApplyAsync(IEnumerable<AnnotationRef>)`, `ApplyPagesAsync(IEnumerable<PageRef>)`, `ApplyAllAsync`, `CanMarkAsync`, `CanApplyAsync`, `IsApplyingAsync`, `GetLastResultAsync`, `ObserveEvents`; `PdfViewer.GetRedactionAsync(DocumentInfo)`.

- [ ] **Step 1: Failing tests.** Port `RedactionTests.swift` case for case (17), plus `aPendingRedactionCountIsInt` into `IntegerSurfaceViewerTests`.
- [ ] **Step 2:** `dotnet test --filter RedactionTests`: build FAIL.
- [ ] **Step 3:** Implement. Calls whose export takes a `CancelToken` use `RunCancellable`.
- [ ] **Step 4:** PASS, 0 warnings.
- [ ] **Step 5:** Flip 29 rows, exclude `page_list_calls`; commit `feat(dotnet): add the redaction capability`.

### Task 2: Measurement

**Files:** create `Plugins/Measurement.cs`, `Plugins/Measurement.Ffi.cs`; modify `Plugins/Measurement.Types.cs` (new twins), `PluginConfigs.cs` (`MeasurementConfig.DefaultScale`, `Presets`); tests `MeasurementTests.cs`, extend `TypedOverloadTests.cs`; manifest, PublicAPI.

**Interfaces (produces):** `MeasurementCapability : IDisposable`, one member per `Measurement.swift` member: settings trio, `CanMeasureAsync(PageRef)`, `GetPageScaleAsync(PageRef)`, `IsBusyAsync`, `GetLastReportsAsync`, `GetPresetsAsync`, `GetUnitsAsync`, `GetAreaUnitsAsync`, the three `ReadoutAsync` overloads, `MeasureDistanceAsync(PageRef, PdfPoint from, PdfPoint to)`, `MeasureAreaAsync(PageRef, IEnumerable<PdfPoint>)`, `GetCalibrationRequestAsync`, `SetScaleAsync(MeasurementPageTarget, PdfMeasurement, MeasurementScaleChangeOptions? options = null)`, `CalibrateAsync(MeasurementCalibrateInput, MeasurementCalibrateOptions? options = null)`, `SetUnitAsync`, `SetAreaUnitAsync`, `SetPrecisionAsync`, `SetPresetAsync`, `ClearScaleAsync`, `StartCalibrationAsync`, `DismissCalibrationAsync`, `CreateMeasurementAsync(CreateMeasurementInput)` (`string`), `CanCalibrateAsync`, `EnsureLoadedAsync(PageRef)`, `ObserveEvents`; `PdfViewer.GetMeasurementAsync(DocumentInfo)`.

- [ ] **Step 1: Failing tests.** Port `MeasurementTests.swift` (17) and the two measurement cases of `TypedOverloadTests.swift` (`AMeasurementReadsOutFromItsAnnotation`, `AMeasurementReadsOutFromItsRef`: 0.0254 within 1e-6, typed equals JSON). Add the fixture `measure-foreign-viewport` (`crates/test-support/fixtures/measure-foreign-viewport.pdf`). New: a `PdfMeasurement` with a NaN `Cyx` or an undefined `MeasureFraction` faults `SetScaleAsync` with `invalid-input`.
- [ ] **Step 2-4:** FAIL, implement, PASS.
- [ ] **Step 5:** Flip 34 rows; commit `feat(dotnet): add the measurement capability`.

### Task 3: Stamp and the file store

**Files:** create `Plugins/Stamp.cs` (capability, accessor, `IStampLibraryStore`, adapter), `Plugins/Stamp.Types.cs`, `Plugins/Stamp.Ffi.cs`, `Plugins/FileStampLibraryStore.cs`; modify `PluginConfigs.cs` (`StampConfig.PreviewWidth`, `Dynamic`); tests `StampTests.cs`, `FileStampLibraryStoreTests.cs`; manifest, PublicAPI.

**Interfaces (produces):** `StampCapability : IDisposable`, one member per `Stamp.swift` member (`GetLibrariesAsync(IEnumerable<string>? kinds = null)`, `GetLibraryAsync`, `GetAssetsAsync(StampAssetFilter? filter = null)`, `GetAssetAsync`, `GetAssetPreviewAsync`, `RenderAssetPreviewAsync(string, double width)`, `GetAssetBytesAsync`, `ExportLibraryAsync`, `CanPlaceAsync(DocumentInfo? document = null)`, `CanCreateFromAnnotationsAsync(DocumentInfo? document = null)`, `CanImportAsync`, `CreateLibraryAsync(string, StampCreateLibraryOptions? options = null)`, `UpdateLibraryAsync`, `ImportLibraryAsync(ReadOnlyMemory<byte>, StampImportOptions? options = null)`, `DeleteLibraryAsync`, `CreateAssetAsync(StampAddAssetInput)`, `CreateAssetFromAnnotationsAsync(IEnumerable<AnnotationRef>, DocumentInfo, PageRef, StampAddAssetInput? input = null)`, `UpdateAssetAsync`, `DeleteAssetAsync`, `MoveAssetAsync(string, string toLibraryId)`, `DuplicateAssetAsync(string, string? label = null)`, `ArmAsync(string, DocumentInfo, double? targetWidth = null)`, `PlaceAsync(string, DocumentInfo, PageRef, PdfPoint center, StampPlacementOptions? options = null)`, `PlaceOnPagesAsync(string, DocumentInfo, IEnumerable<PageRef>? pages, PdfPoint center, StampPlacementOptions? options = null)`, `DisarmAsync(DocumentInfo)`, `GetArmedAssetAsync(DocumentInfo)`, `RestoreAsync(IStampLibraryStore)`, `PersistAsync(IStampLibraryStore, IEnumerable<string>? except = null)` (`Registration`), settings trio, `ObserveEvents`); `PdfViewer.GetStampsAsync()`; `StampPlacementOptions { double? TargetWidth; double? Rotation }` (RS0026).

- [ ] **Step 1: Failing tests.** Port `StampTests.swift` (31), `stampImportOptionsIdNamesTheLibrary` and `stampResetRestoresRegisteredPreviewWidth`. Fixture `stamps` (`packages/default-stamps/en/stamps.pdf`, read only). New `FileStampLibraryStoreTests`: put/list/delete round trip; `AHostileIdStaysInTheDirectory` (ids `../x`, `a/b`, `c:d`, `.`) each lists back as the same id and nothing appears outside the directory; a missing delete is no error; the default root is under `LocalApplicationData`; a store whose `List` throws fails `RestoreAsync` with `HostError`; a store that calls `viewer.Dispose()` records `InvalidOperationException`.
- [ ] **Step 2-4:** FAIL, implement, PASS.
- [ ] **Step 5:** Flip 39 rows; commit `feat(dotnet): add the stamp capability and the file stamp store`.

### Task 4: Signature

**Files:** create `Plugins/Signature.cs` (capability, accessor, viewer setters), `Plugins/Signature.Ports.cs` (`ISignatureSigner`, `ISignatureTrust`, `ISignatureTransport`, `SignatureTransportOutcome`, adapters), `Plugins/Signature.Types.cs`, `Plugins/Signature.Ffi.cs`; modify `PluginConfigs.cs` (`SignatureConfig` fields), `WireInteger.cs` (`ToByte`); tests `SignatureTests.cs`, `TestSigner.cs` (the TEST-ONLY RSA signer from `packages/engine/main/test/fixtures/signature-compat/keys/` through Task 5's API is not yet there, so this task's helper signs with `RSA` directly), extend `IntegerSurfaceViewerTests.cs`; manifest, PublicAPI.

**Interfaces (produces):** `SignatureCapability : IDisposable`, one member per `Signature.swift` member (`GetSnapshotAsync`, `GetStatusAsync`, `GetSignaturesAsync`, `GetUnsignedFieldsAsync`, `GetSignatureAsync(FormFieldRef)`, `GetVerdictsAsync`, `GetVerdictAsync`, `GetProtectionAsync`, `GetPendingAsync`, `IsBusyAsync`, `GetModeAsync`, `GetTargetAsync`, `CanSignAsync`, `CanFillAsync`, `CanCertifyAsync`, `CanReadRevisionAsync`, `RefreshAsync`, `ValidateAsync(SignatureValidateRequest? request = null)`, `ValidateFieldAsync(FormFieldRef, SignatureValidateRequest? request = null)`, `AnalyzeChangesAsync(int since, SignatureModificationsBasis until = WorkingCopy)`, `ReadRevisionAsync`, `SetTargetAsync(FormFieldRef?)`, `RequestInspectionAsync`, `SignAsync(SignatureSignInput, ISignatureSigner? signer = null)`, `PrepareSignatureAsync`, `CompleteSignatureAsync(string, ReadOnlyMemory<byte> cms)`, `CancelPendingAsync`, `FillAsync(FormFieldRef, SignatureMark)`, `ClearFieldAsync`, `PlaceAsync(SignatureMark, SignaturePlaceTarget)`, `AddValidationInfoAsync(FormFieldRef? field = null)`, `CanAddValidationInfoAsync`, settings trio, `ObserveEvents`); `PdfViewer.GetSignaturesAsync`, `SetSigner`, `SetTrust`, `SetSignatureTransport`.

- [ ] **Step 1: Failing tests.** Port `SignatureTests.swift` (35, with `CancellingSigner` throwing `PdfException` `operation-cancelled` and `HeldSigner` over a `TaskCompletionSource`), `signatureModeCanClearAndResetToRegisteredMode`, `theSignatureSettingsCarryTheNewFields`, `aSignatureIndexOutsideUInt32ThrowsInvalidInput`. Fixtures `signed_certified`, `unsigned_sigfield`, `signature_artwork` from `packages/engine/main/test/fixtures/`. New: a signer whose chain throws fails `SignAsync` with that `PdfException`'s code; a signer that calls `viewer.Dispose()` from `GetCertificateChain` records `InvalidOperationException`; `SignAsync` returning a null task fails the signing.
- [ ] **Step 2-4:** FAIL, implement, PASS.
- [ ] **Step 5:** Flip 39 rows; commit `feat(dotnet): add the signature capability`.

### Task 5: X509 signer and trust anchors

**Files:** create `Plugins/X509CertificateSigner.cs`, `Plugins/TrustAnchors.cs`; tests `X509CertificateSignerTests.cs` (port of `KeychainSignerTests.swift`); embed `TEST-ONLY-signer.pem` as a test resource; switch `TestSigner` to `X509CertificateSigner`; PublicAPI.

- [ ] **Step 1: Failing tests.** An RSA key signs PKCS#1 (256 bytes, `RsaPkcs1`, `Sha256`, chain of 1); an EC P-256 key signs 64-byte `r || s` that `ECDsa.VerifyData` accepts in P1363; a certificate without a private key throws `ArgumentException`; a malformed PEM anchor throws `CryptographicException`; a PEM anchor's DER starts `0x30`; embedded-resource anchors load from the test assembly; a PKCS#12 round trip from bytes signs; a chain of leaf plus issuer builds leaf first; `TheTestCertificateAsAnAnchorIsTrusted` passes through `X509CertificateSigner` and `TrustAnchors`.
- [ ] **Step 2-4:** FAIL, implement, PASS.
- [ ] **Step 5:** Commit `feat(dotnet): add the X509 certificate signer and trust anchors`.

### Task 6: HTTP signature transport

**Files:** create `HttpClientSignatureTransport.cs`; tests `SignatureTransportTests.cs` over a loopback `TcpListener` server (the routes of the Swift server: `/echo`, `/hold`, `/stall-body`, `/error-stall`, `/broken`, `/error-broken`, `/long-length`, `/long-stream`, `/exact`, `/error-long`, `/error-body`); PublicAPI.

- [ ] **Step 1: Failing tests.** Port `SignatureTransportTests.swift`: `AReplyOfAnyStatusIsReplied`, `TheDeadlineCoversTheWholeExchangeAndAnswersOnce` (250 ms to 1400 ms, one answer each after 1.7 s), `MaxReplyBytesIsEnforced`, `NothingListeningOrNoAddressIsFailed`, `PostAnswersThroughTheGeneratedCompletion` (one POST reaches `/error-body`, signing fails), the three route tests skipped without `EMBEDPDF_SIGNATURE_ROUTES`. The catch-up cases already ported in waves B to D are noted, not repeated. New: a transport that throws or returns a null task answers `Failed` once.
- [ ] **Step 2-4:** FAIL, implement, PASS.
- [ ] **Step 5:** Commit `feat(dotnet): add the HTTP signature transport`.

### Task 7: Cross-cutting tests

- [ ] `DisabledPluginTests`: the four rows call the public accessors.
- [ ] `NullArgumentTests.EveryWaveECapabilityChecksEveryReferenceArgument`; null elements in `UnmarkAsync`, `ApplyAsync`, `MeasureAreaAsync`, `CreateAssetFromAnnotationsAsync` throw `ArgumentException` up front.
- [ ] `ValidationShapeTests.InvalidWaveEArgumentsFaultTheReturnedTask`: a negative `AnalyzeChangesAsync` index, an undefined `SignatureModificationsBasis`, a NaN draft-free `MarkAreaAsync` rect, a bad `PageRef`.
- [ ] `EnumMirrorTests` and `EnumRangeTests` rows for every new enum.
- [ ] `LifetimeTests.LiveWaveEStreamsAndCallbacksDoNotKeepAnAbandonedViewerAlive` (review focus 5).
- [ ] Run, PASS; commit `test(dotnet): extend the cross-cutting tests to wave E`.

### Task 8: Verify and record

- [ ] `python3 scripts/check-api-coverage.py --platform csharp` exits 0 with no pending row.
- [ ] `dotnet test EmbedPDF.Viewer.Tests` twice, `python3 -m unittest discover -s platforms/dotnet/packaging`, `python3 -m unittest scripts/test_check_api_coverage.py`, `dotnet build -c Release` of the facade, DI, Jint, tests and ApiDump with 0 warnings.
- [ ] Tick this plan; add "Phase 3 wave E notes" to the umbrella and tick its Phase 3 exit. Commit `chore(dotnet): record Phase 3 wave E`.
