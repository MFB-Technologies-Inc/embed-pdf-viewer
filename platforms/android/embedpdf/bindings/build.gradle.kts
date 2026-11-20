plugins {
    alias(libs.plugins.android.library)
}

// The repository root, which holds the Cargo workspace this module's native
// library and generated Kotlin come from.
val repoRoot: File = rootDir.resolve("../../..").canonicalFile

android {
    namespace = "com.embedpdf.bindings"
    compileSdk = 36

    defaultConfig {
        // Matches API in build-aar.sh. A lower floor here than the one the .so
        // was linked against would ship a library that fails to resolve
        // symbols on old devices rather than refusing to install.
        minSdk = 24
        ndk {
            abiFilters += listOf("arm64-v8a", "x86_64")
        }
        // Shipped to consumers rather than applied here: the library is not
        // what runs the shrinker, the app is.
        consumerProguardFiles("consumer-rules.pro")
    }

    // build-aar.sh puts the .so files in src/main/jniLibs and the generated
    // Kotlin in src/main/kotlin, the default source directories. Neither is
    // committed.

    packaging {
        jniLibs {
            // Stored rather than deflated, so Android's linker can mmap the
            // library straight out of the APK. Compressing it also defeats the
            // 16 KB alignment the ELF was built with.
            useLegacyPackaging = false
        }
    }

    buildTypes {
        release {
            isMinifyEnabled = false
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
}

kotlin {
    compilerOptions {
        jvmTarget = org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_17
    }
}

dependencies {
    // uniffi's Kotlin backend calls the .so through JNA, so the AAR variant
    // has to be used: the plain jar cannot find a library inside an APK. The
    // generated classes take and return JNA types, so consumers compile
    // against it too.
    api(libs.jna) { artifact { type = "aar" } }
    implementation(libs.kotlinx.coroutines.core)

    // The JVM tests load the Linux host library, which needs the jar's
    // desktop dispatch library rather than the AAR's Android ones.
    testImplementation(libs.jna)
    testImplementation(libs.junit)
    testImplementation(libs.kotlinx.coroutines.core)
}

// The full viewer host for the machine running the tests. The generated
// Kotlin in src/main/kotlin and this library come from the same sources, so
// their uniffi checksums agree; a stale build fails loudly at the first call.
// `-PlinuxHostDir=<dir>` tests an already-built library instead.
val prebuiltHostDir = providers.gradleProperty("linuxHostDir").orNull
// Where cargo writes, as cargo reads it: CARGO_TARGET_DIR, relative to the
// workspace when it is relative, or the workspace's `target`.
val cargoTargetDir: File = System.getenv("CARGO_TARGET_DIR")?.takeIf { it.isNotEmpty() }
    ?.let { repoRoot.resolve(it) } ?: repoRoot.resolve("target")
val linuxHostDir = prebuiltHostDir?.let(::File) ?: cargoTargetDir.resolve("debug")
val runtimeSlice = if (System.getProperty("os.arch") == "aarch64") "linux-arm64" else "linux-x64"
// fetch-android-runtime.sh and the Rust build read PDF_RUNTIME_LIB_DIR too.
val runtimeRoot: File = System.getenv("PDF_RUNTIME_LIB_DIR")?.takeIf { it.isNotEmpty() }
    ?.let(::File) ?: repoRoot.resolve("packages/engine/runtime/build/libpdfium")
val runtimeLibDir = runtimeRoot.resolve("$runtimeSlice/lib")
// Read by the modules whose JVM tests load the same library.
extra["linuxHostDir"] = linuxHostDir.path
extra["runtimeLibDir"] = runtimeLibDir.path
extra["cargoTargetDir"] = cargoTargetDir.path

val hostLibrary = linuxHostDir.resolve("libepdf_viewer_host.so")
val bindgen = cargoTargetDir.resolve("debug/uniffi-bindgen")

val buildLinuxHost = tasks.register<Exec>("buildLinuxHost") {
    description = "Builds epdf-viewer-host and uniffi-bindgen for the JVM tests."
    workingDir = repoRoot
    val packages = if (prebuiltHostDir == null) listOf("-p", "epdf-viewer-host") else emptyList()
    commandLine(
        listOf("cargo", "build") + packages +
            listOf("-p", "uniffi-bindgen", "--manifest-path", repoRoot.resolve("Cargo.toml").path),
    )
}

// build-aar.sh generates src/main/kotlin from the Android library for the
// AAR. A checkout that never ran it, or whose Rust changed since, gets the
// same Kotlin from the Linux library here, so the JVM tests need no script
// first. Kotlin newer than both the library and the generator is left alone.
val kotlinSources: File = layout.projectDirectory.dir("src/main/kotlin").asFile
val generateLinuxBindings = tasks.register<Exec>("generateLinuxBindings") {
    description = "Generates the uniffi Kotlin from the Linux host library when it is missing or stale."
    dependsOn(buildLinuxHost)
    inputs.files(hostLibrary, bindgen)
    onlyIf("the generated Kotlin is missing or older than the library or uniffi-bindgen") {
        val newest = kotlinSources.walk().filter { it.isFile }.maxOfOrNull { it.lastModified() }
        newest == null || newest < maxOf(hostLibrary.lastModified(), bindgen.lastModified())
    }
    doFirst {
        // Fail before deleting anything the generator could not replace.
        for (input in listOf(hostLibrary, bindgen)) {
            if (!input.isFile) throw GradleException("generateLinuxBindings: $input is missing; the Kotlin is left as it was")
        }
        kotlinSources.deleteRecursively()
    }
    commandLine(
        bindgen.path, "generate", "--library", hostLibrary.path,
        "--language", "kotlin", "--no-format", "--out-dir", kotlinSources.path,
    )
}

// Every Kotlin compile, the release one too, since `:embedpdf:apiCheck` dumps
// the release classes.
tasks.matching { it.name.startsWith("compile") && it.name.endsWith("Kotlin") }.configureEach {
    dependsOn(generateLinuxBindings)
}

tasks.withType<Test>().configureEach {
    dependsOn(buildLinuxHost)
    // Set here rather than by the caller's shell, so a test run from any
    // shell or IDE loads the library this build made.
    systemProperty("jna.library.path", linuxHostDir.path)
    systemProperty("embedpdf.fixtures", repoRoot.resolve("pdfium-multiplatform-prototype/fixtures").path)
    environment("LD_LIBRARY_PATH", runtimeLibDir.path)
    testLogging {
        events("passed", "failed", "skipped")
        exceptionFormat = org.gradle.api.tasks.testing.logging.TestExceptionFormat.FULL
    }
}
