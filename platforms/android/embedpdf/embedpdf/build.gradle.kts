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
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
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
    implementation(libs.androidx.tracing)

    testImplementation(libs.jna)
    testImplementation(libs.junit)
    testImplementation(libs.kotlinx.coroutines.test)
    testImplementation(libs.robolectric)
}

// The JVM tests load the Linux host library `:bindings` builds for them.
val linuxHost = project(":bindings").extensions.extraProperties

tasks.withType<Test>().configureEach {
    dependsOn(":bindings:buildLinuxHost")
    // One JVM per test class. A Robolectric class loads the generated
    // bindings again in its sandbox class loader, and that copy registers its
    // own callback tables with the one native library. A plain test that runs
    // after it in the same JVM then has its callbacks (a fetch cancellation,
    // say) answered from the sandbox's empty handle maps, and the process
    // crashes in native code.
    forkEvery = 1
    systemProperty("jna.library.path", linuxHost["linuxHostDir"].toString())
    systemProperty(
        "embedpdf.fixtureDirs",
        listOf(
            "pdfium-multiplatform-prototype/fixtures",
            "tooling/viewer-e2e/fixtures",
            "tooling/engine-conformance/fixtures",
            "packages/engine/main/test/fixtures",
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
