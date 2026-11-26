// Compose views over the models, published as `embedpdf-compose`: the Kotlin
// counterpart of `EmbedPDFUI`. Each composable takes a model from
// `embedpdf-models` and draws it with Material 3.
plugins {
    alias(libs.plugins.android.library)
    alias(libs.plugins.kotlin.compose)
}

val repoRoot: File = rootDir.resolve("../../..").canonicalFile

// The tests read the Linux host library's location from `:bindings`.
evaluationDependsOn(":bindings")

android {
    namespace = "com.embedpdf.compose"
    compileSdk = 36

    defaultConfig {
        minSdk = 24
    }

    buildFeatures {
        compose = true
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    sourceSets {
        // The facade's fixtures and EmbedPdfTestRunner.
        getByName("test").kotlin.srcDir("../embedpdf/src/testShared/kotlin")
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
    // Every composable takes a model and exposes facade types.
    api(project(":models"))
    api(platform(libs.compose.bom))
    api(libs.compose.ui)
    api(libs.compose.ui.graphics)
    api(libs.compose.foundation)
    api(libs.compose.material3)
    implementation(libs.kotlinx.coroutines.android)

    testImplementation(platform(libs.compose.bom))
    testImplementation(libs.compose.ui.test.junit4)
    // The activity `createComposeRule` launches.
    testImplementation(libs.compose.ui.test.manifest)
    testImplementation(libs.jna)
    testImplementation(libs.junit)
    testImplementation(libs.kotlinx.coroutines.test)
    testImplementation(libs.robolectric)
    testImplementation(libs.androidx.test.core)
}

// The JVM tests load the Linux host library `:bindings` builds for them.
val linuxHost = project(":bindings").extensions.extraProperties

tasks.withType<Test>().configureEach {
    dependsOn(":bindings:buildLinuxHost")
    systemProperty("jna.library.path", linuxHost["linuxHostDir"].toString())
    systemProperty(
        "embedpdf.fixtureDirs",
        listOf(
            "pdfium-multiplatform-prototype/fixtures",
            "tooling/viewer-e2e/fixtures",
            "tooling/engine-conformance/fixtures",
            "packages/engine/main/test/fixtures",
            "crates/test-support/fixtures",
        ).joinToString(File.pathSeparator) { repoRoot.resolve(it).path },
    )
    environment("LD_LIBRARY_PATH", linuxHost["runtimeLibDir"].toString())
    // Compose under Robolectric, with native graphics for the pixel tests, outgrows the default heap.
    maxHeapSize = "3g"
    testLogging {
        events("passed", "failed", "skipped")
        exceptionFormat = org.gradle.api.tasks.testing.logging.TestExceptionFormat.FULL
    }
}

// The public surface, dumped to api/compose.api, the same way
// `embedpdf/build.gradle.kts` dumps the facade's.
val abiRuntime: Configuration = configurations.create("abiRuntime") {
    isCanBeConsumed = false
}

dependencies {
    abiRuntime(libs.asm)
    abiRuntime(libs.asm.tree)
    abiRuntime(libs.kotlin.metadata.jvm)
}

val apiFile = layout.projectDirectory.file("api/compose.api")

val apiBuild = tasks.register<kotlinx.validation.KotlinApiBuildTask>("apiBuild") {
    description = "Builds the public API dump of the release classes."
    val compile = tasks.named<org.jetbrains.kotlin.gradle.tasks.KotlinCompile>("compileReleaseKotlin")
    inputClassesDirs.from(compile.flatMap { it.destinationDirectory })
    outputApiFile.set(layout.buildDirectory.file("api/compose.api"))
    runtimeClasspath.from(abiRuntime)
}

tasks.register<Copy>("apiDump") {
    description = "Writes the public API dump to api/compose.api."
    group = "verification"
    from(apiBuild.flatMap { it.outputApiFile })
    into(apiFile.asFile.parentFile)
}

val apiCheck = tasks.register<kotlinx.validation.KotlinApiCompareTask>("apiCheck") {
    description = "Fails when the public API differs from api/compose.api."
    group = "verification"
    projectApiFile.set(apiFile)
    generatedApiFile.set(apiBuild.flatMap { it.outputApiFile })
}

tasks.named("check") { dependsOn(apiCheck) }
