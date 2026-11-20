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
val linuxHostDir = repoRoot.resolve("target/debug")
val runtimeSlice = if (System.getProperty("os.arch") == "aarch64") "linux-arm64" else "linux-x64"
val runtimeLibDir = repoRoot.resolve("packages/engine/runtime/build/libpdfium/$runtimeSlice/lib")

val buildLinuxHost by tasks.registering(Exec::class) {
    description = "Builds epdf-viewer-host for the JVM tests."
    workingDir = repoRoot
    commandLine("cargo", "build", "-p", "epdf-viewer-host", "--manifest-path", repoRoot.resolve("Cargo.toml").path)
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
