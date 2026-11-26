// Observable models over the facade, published as `embedpdf-models`: the
// Kotlin counterpart of `EmbedPDFModels`. Each model exposes `StateFlow`s and
// works from a View-based app or from Compose; the module itself takes no
// Compose dependency, which `checkNoCompose` enforces.
plugins {
    alias(libs.plugins.android.library)
}

val repoRoot: File = rootDir.resolve("../../..").canonicalFile

// The tests read the Linux host library's location from `:bindings`.
evaluationDependsOn(":bindings")

android {
    namespace = "com.embedpdf.models"
    compileSdk = 36

    defaultConfig {
        minSdk = 24
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
    // Every model takes facade capabilities and exposes facade types.
    api(project(":embedpdf"))
    // `PdfViewerViewModel` is an `AndroidViewModel`.
    api(libs.androidx.lifecycle.viewmodel)
    // `Dispatchers.Main`, where every model keeps its state.
    implementation(libs.kotlinx.coroutines.android)

    testImplementation(libs.jna)
    testImplementation(libs.junit)
    testImplementation(libs.kotlinx.coroutines.test)
    testImplementation(libs.robolectric)
    testImplementation(libs.androidx.activity)
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
    testLogging {
        events("passed", "failed", "skipped")
        exceptionFormat = org.gradle.api.tasks.testing.logging.TestExceptionFormat.FULL
    }
}

// No Compose: neither a dependency on any `androidx.compose` artifact, direct
// or transitive, nor an `androidx.compose` import in the module's sources.
val checkNoCompose = tasks.register("checkNoCompose") {
    description = "Fails when an androidx.compose dependency or import appears in the module."
    group = "verification"
    val classpaths = listOf("releaseRuntimeClasspath", "releaseCompileClasspath", "debugRuntimeClasspath")
        .map { name -> configurations.named(name).flatMap { it.incoming.resolutionResult.rootComponent } }
    val sources = fileTree("src/main") { include("**/*.kt", "**/*.java") }
    inputs.files(sources)
    doLast {
        val composeModules = classpaths.flatMap { root ->
            val found = mutableSetOf<String>()
            val seen = mutableSetOf<org.gradle.api.artifacts.result.ResolvedComponentResult>()
            fun walk(component: org.gradle.api.artifacts.result.ResolvedComponentResult) {
                if (!seen.add(component)) return
                val id = component.moduleVersion
                if (id != null && id.group.startsWith("androidx.compose")) found += "${id.group}:${id.name}"
                component.dependencies
                    .filterIsInstance<org.gradle.api.artifacts.result.ResolvedDependencyResult>()
                    .forEach { walk(it.selected) }
            }
            walk(root.get())
            found
        }.toSortedSet()
        val imports = sources.files.flatMap { file ->
            file.readLines().withIndex()
                .filter { (_, line) -> line.trimStart().startsWith("import androidx.compose") }
                .map { (index, _) -> "${file.relativeTo(projectDir)}:${index + 1}" }
        }
        if (composeModules.isNotEmpty() || imports.isNotEmpty()) {
            throw GradleException(
                "The models module takes no Compose. Dependencies: $composeModules. Imports: $imports.",
            )
        }
    }
}

tasks.named("check") { dependsOn(checkNoCompose) }
tasks.named("preBuild") { dependsOn(checkNoCompose) }

// The public surface, dumped to api/models.api, the same way
// `embedpdf/build.gradle.kts` dumps the facade's.
val abiRuntime: Configuration = configurations.create("abiRuntime") {
    isCanBeConsumed = false
}

dependencies {
    abiRuntime(libs.asm)
    abiRuntime(libs.asm.tree)
    abiRuntime(libs.kotlin.metadata.jvm)
}

val apiFile = layout.projectDirectory.file("api/models.api")

val apiBuild = tasks.register<kotlinx.validation.KotlinApiBuildTask>("apiBuild") {
    description = "Builds the public API dump of the release classes."
    val compile = tasks.named<org.jetbrains.kotlin.gradle.tasks.KotlinCompile>("compileReleaseKotlin")
    inputClassesDirs.from(compile.flatMap { it.destinationDirectory })
    outputApiFile.set(layout.buildDirectory.file("api/models.api"))
    runtimeClasspath.from(abiRuntime)
}

tasks.register<Copy>("apiDump") {
    description = "Writes the public API dump to api/models.api."
    group = "verification"
    from(apiBuild.flatMap { it.outputApiFile })
    into(apiFile.asFile.parentFile)
}

val apiCheck = tasks.register<kotlinx.validation.KotlinApiCompareTask>("apiCheck") {
    description = "Fails when the public API differs from api/models.api."
    group = "verification"
    projectApiFile.set(apiFile)
    generatedApiFile.set(apiBuild.flatMap { it.outputApiFile })
}

tasks.named("check") { dependsOn(apiCheck) }
