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
    // The BOM picks the versions. As an `implementation` platform it stays
    // off a consumer's compile classpath, but its constraints still reach
    // the app's runtime classpath, where they can raise the app's own
    // Compose artifacts to the BOM's versions (never lower them). The
    // artifacts the public signatures use (`Modifier`, `Color`,
    // `Orientation`) stay `api`; a publication must carry the versions
    // resolved from the BOM for them, since the catalog names none.
    implementation(platform(libs.compose.bom))
    api(libs.compose.ui)
    api(libs.compose.ui.graphics)
    api(libs.compose.foundation)
    // No public signature names a Material 3 type: the composables read
    // `MaterialTheme` only inside.
    implementation(libs.compose.material3)
    implementation(libs.compose.material3.adaptive)
    implementation(libs.androidx.activity.compose)
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
    // The Compose compiler makes a public `ComposableSingletons$<File>Kt`
    // holder for each file's lambdas, named by hash. It is no API anyone
    // calls, and its members change with any edit to a lambda, so the dump
    // leaves those classes out.
    doLast {
        val dump = outputApiFile.get().asFile
        val blocks = dump.readText().split("\n\n")
        dump.writeText(blocks.filterNot { it.trimStart().startsWith("public final class com/embedpdf/compose/ComposableSingletons$") }.joinToString("\n\n"))
    }
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

// What a consumer's compile classpath gets from this module: the BOM and
// Material 3 must not be among it (see the dependencies above).
val checkApiDependencies = tasks.register("checkApiDependencies") {
    description = "Fails when the Compose BOM or an androidx.compose.material3 artifact is an api dependency."
    group = "verification"
    val declared = configurations.named("releaseApiElements").map { elements ->
        elements.allDependencies.map { (it.group ?: "") to it.name }
    }
    doLast {
        // Every group under androidx.compose.material3, the adaptive layouts included.
        val leaked = declared.get()
            .filter { (group, name) -> (group == "androidx.compose" && name == "compose-bom") || group.startsWith("androidx.compose.material3") }
            .map { (group, name) -> "$group:$name" }
        check(leaked.isEmpty()) { "api dependencies a consumer would inherit: $leaked" }
    }
}

tasks.named("check") { dependsOn(apiCheck, checkApiDependencies) }
