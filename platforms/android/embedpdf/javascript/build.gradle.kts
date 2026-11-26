// PDF JavaScript in the platform's JavaScript sandbox, published as
// `embedpdf-javascript`: `JavaScriptSandboxEngineFactory`. It is a module of
// its own so that only an app that wants PDF JavaScript takes
// `androidx.javascriptengine`, whose minSdk 26 this module's manifest
// overrides.
plugins {
    alias(libs.plugins.android.library)
}

val repoRoot: File = rootDir.resolve("../../..").canonicalFile

// The tests read the Linux host library's location from `:bindings`.
evaluationDependsOn(":bindings")

android {
    namespace = "com.embedpdf.javascript"
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
        // The facade's fixtures and EmbedPdfTestRunner.
        getByName("test").kotlin.srcDir("../embedpdf/src/testShared/kotlin")
        getByName("androidTest").kotlin.srcDir("../embedpdf/src/androidTestShared/kotlin")
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
    // The factory is passed to `ActionsJavaScriptConfig`, a facade type.
    api(project(":embedpdf"))
    implementation(libs.androidx.javascriptengine)

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
    from(repoRoot.resolve("packages/engine/main/test/fixtures")) { include("action_buttons_form.pdf") }
    from(repoRoot.resolve("tooling/engine-conformance/fixtures")) { include("document_actions.pdf") }
    into(layout.buildDirectory.dir("generated/deviceFixtures/fixtures"))
}
android.sourceSets.getByName("androidTest").assets.srcDir(layout.buildDirectory.dir("generated/deviceFixtures").get().asFile)
tasks.matching { it.name.endsWith("AndroidTestAssets") }.configureEach { dependsOn(deviceFixtures) }

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
        ).joinToString(File.pathSeparator) { repoRoot.resolve(it).path },
    )
    environment("LD_LIBRARY_PATH", linuxHost["runtimeLibDir"].toString())
    testLogging {
        events("passed", "failed", "skipped")
        exceptionFormat = org.gradle.api.tasks.testing.logging.TestExceptionFormat.FULL
    }
}

// The public surface, dumped to api/javascript.api, the same way
// `embedpdf/build.gradle.kts` dumps the facade's.
val abiRuntime: Configuration = configurations.create("abiRuntime") {
    isCanBeConsumed = false
}

dependencies {
    abiRuntime(libs.asm)
    abiRuntime(libs.asm.tree)
    abiRuntime(libs.kotlin.metadata.jvm)
}

val apiFile = layout.projectDirectory.file("api/javascript.api")

val apiBuild = tasks.register<kotlinx.validation.KotlinApiBuildTask>("apiBuild") {
    description = "Builds the public API dump of the release classes."
    val compile = tasks.named<org.jetbrains.kotlin.gradle.tasks.KotlinCompile>("compileReleaseKotlin")
    inputClassesDirs.from(compile.flatMap { it.destinationDirectory })
    outputApiFile.set(layout.buildDirectory.file("api/javascript.api"))
    runtimeClasspath.from(abiRuntime)
}

tasks.register<Copy>("apiDump") {
    description = "Writes the public API dump to api/javascript.api."
    group = "verification"
    from(apiBuild.flatMap { it.outputApiFile })
    into(apiFile.asFile.parentFile)
}

val apiCheck = tasks.register<kotlinx.validation.KotlinApiCompareTask>("apiCheck") {
    description = "Fails when the public API differs from api/javascript.api."
    group = "verification"
    projectApiFile.set(apiFile)
    generatedApiFile.set(apiBuild.flatMap { it.outputApiFile })
}

tasks.named("check") { dependsOn(apiCheck) }
