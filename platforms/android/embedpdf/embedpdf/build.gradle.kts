// The hand-written Kotlin facade over the generated bindings: `PdfViewer`,
// `PdfException` and the `com.embedpdf` names an app writes against.
plugins {
    alias(libs.plugins.android.library)
    // `@Serializable` types: the tests' shell props now, the typed annotation
    // drafts later.
    alias(libs.plugins.kotlin.serialization)
}

val repoRoot: File = rootDir.resolve("../../..").canonicalFile

// The tests read the Linux host library's location from `:bindings`.
evaluationDependsOn(":bindings")

android {
    namespace = "com.embedpdf"
    compileSdk = 36

    defaultConfig {
        minSdk = 24
        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    sourceSets {
        // Fixtures and runners the javascript module's tests share.
        getByName("test").kotlin.srcDir("src/testShared/kotlin")
        // Fixture access the javascript and compose modules' device tests share.
        getByName("androidTest").kotlin.srcDir("src/androidTestShared/kotlin")
    }

    testOptions {
        unitTests {
            // Robolectric reads the merged manifest and resources.
            isIncludeAndroidResources = true
        }
    }
}

kotlin {
    explicitApi()
    compilerOptions {
        jvmTarget = org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_17
    }
}

dependencies {
    // The facade re-exports generated records through typealiases, so a
    // consumer compiles against the bindings too.
    api(project(":bindings"))
    api(libs.kotlinx.coroutines.core)
    // Typed shell props encode through it, so a caller's serializers do too.
    api(libs.kotlinx.serialization.json)
    // `@ColorInt` on the annotation colour conversions.
    implementation(libs.androidx.annotation)
    implementation(libs.androidx.tracing)

    testImplementation(libs.jna)
    testImplementation(libs.junit)
    testImplementation(libs.kotlinx.coroutines.test)
    testImplementation(libs.robolectric)

    androidTestImplementation(libs.androidx.test.runner)
    androidTestImplementation(libs.androidx.test.ext.junit)
    androidTestImplementation(libs.junit)
    androidTestImplementation(libs.kotlinx.coroutines.test)
}

// The instrumented tests (src/androidTest, `./gradlew connectedDebugAndroidTest`)
// read their fixtures from the test APK's assets, under `fixtures/`.
val deviceFixtures = tasks.register<Sync>("deviceFixtures") {
    from(repoRoot.resolve("pdfium-multiplatform-prototype/fixtures")) { include("report.pdf", "encrypted-hello.pdf") }
    from(repoRoot.resolve("packages/engine/main/test/fixtures")) { include("unsigned_sigfield.pdf", "signature_artwork.pdf") }
    from(repoRoot.resolve("packages/engine/main/test/fixtures/signature-compat/keys")) { include("TEST-ONLY-signer.pem", "TEST-ONLY-private-key.pem") }
    into(layout.buildDirectory.dir("generated/deviceFixtures/fixtures"))
}
android.sourceSets.getByName("androidTest").assets.srcDir(layout.buildDirectory.dir("generated/deviceFixtures").get().asFile)
tasks.matching { it.name.endsWith("AndroidTestAssets") }.configureEach { dependsOn(deviceFixtures) }

// The JVM tests load the Linux host library `:bindings` builds for them.
val linuxHost = project(":bindings").extensions.extraProperties

tasks.withType<Test>().configureEach {
    dependsOn(":bindings:buildLinuxHost")
    // Robolectric classes share the JVM with plain ones: EmbedPdfTestRunner
    // keeps the generated bindings out of Robolectric's sandbox class loader.
    systemProperty("jna.library.path", linuxHost["linuxHostDir"].toString())
    // SignatureRoutesTest starts platforms/android/tests/signature-routes.mjs from here.
    systemProperty("embedpdf.repoRoot", repoRoot.path)
    systemProperty(
        "embedpdf.fixtureDirs",
        listOf(
            "pdfium-multiplatform-prototype/fixtures",
            "tooling/viewer-e2e/fixtures",
            "tooling/engine-conformance/fixtures",
            "packages/engine/main/test/fixtures",
            "crates/test-support/fixtures",
            // The shipped English stamp library, as `stamps.pdf`.
            "packages/default-stamps/en",
            // The test-only signing key and certificate.
            "packages/engine/main/test/fixtures/signature-compat/keys",
        ).joinToString(File.pathSeparator) { repoRoot.resolve(it).path },
    )
    environment("LD_LIBRARY_PATH", linuxHost["runtimeLibDir"].toString())
    // PointerLatencyTest runs only when asked for.
    System.getenv("EMBEDPDF_LATENCY")?.let { environment("EMBEDPDF_LATENCY", it) }
    testLogging {
        events("passed", "failed", "skipped")
        exceptionFormat = org.gradle.api.tasks.testing.logging.TestExceptionFormat.FULL
    }
}

// The public surface, dumped to api/embedpdf.api. The binary-compatibility
// validator wires its tasks to the Kotlin Android plugin, which AGP's built-in
// Kotlin replaces, so its tasks are registered here over the release classes.
val abiRuntime: Configuration = configurations.create("abiRuntime") {
    isCanBeConsumed = false
}

dependencies {
    abiRuntime(libs.asm)
    abiRuntime(libs.asm.tree)
    abiRuntime(libs.kotlin.metadata.jvm)
}

val apiFile = layout.projectDirectory.file("api/embedpdf.api")

val apiBuild = tasks.register<kotlinx.validation.KotlinApiBuildTask>("apiBuild") {
    description = "Builds the public API dump of the release classes."
    val compile = tasks.named<org.jetbrains.kotlin.gradle.tasks.KotlinCompile>("compileReleaseKotlin")
    inputClassesDirs.from(compile.flatMap { it.destinationDirectory })
    outputApiFile.set(layout.buildDirectory.file("api/embedpdf.api"))
    runtimeClasspath.from(abiRuntime)
}

tasks.register<Copy>("apiDump") {
    description = "Writes the public API dump to api/embedpdf.api."
    group = "verification"
    from(apiBuild.flatMap { it.outputApiFile })
    into(apiFile.asFile.parentFile)
}

val apiCheck = tasks.register<kotlinx.validation.KotlinApiCompareTask>("apiCheck") {
    description = "Fails when the public API differs from api/embedpdf.api."
    group = "verification"
    projectApiFile.set(apiFile)
    generatedApiFile.set(apiBuild.flatMap { it.outputApiFile })
}

tasks.named("check") { dependsOn(apiCheck) }
