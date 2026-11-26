plugins {
    // AGP 9 bundles Kotlin support, so applying org.jetbrains.kotlin.android on
    // top of it fails: both register a `kotlin` extension. Declaring it here
    // without applying it only puts that Kotlin Gradle plugin on the build
    // classpath, which is how the compiler version is raised past AGP's own.
    alias(libs.plugins.android.library) apply false
    alias(libs.plugins.kotlin.android) apply false
    alias(libs.plugins.kotlin.jvm) apply false
    alias(libs.plugins.kotlin.serialization) apply false
    alias(libs.plugins.kotlin.compose) apply false
    // Puts the API dump tasks `embedpdf/build.gradle.kts` registers on the
    // classpath; its `apiValidation` extension holds their settings.
    alias(libs.plugins.binary.compatibility.validator)
}

apiValidation {
    // bindings is generated and versioned by engine hash; smoke is a test.
    ignoredProjects += listOf("bindings", "smoke")
}

// Publishing. The five library modules publish as `com.embedpdf` artifacts
// with `maven-publish`: `./gradlew publishToMavenLocal` locally, and
// `publishAllPublicationsToGitHubPackagesRepository` with
// `-Pembedpdf.packagesRepository=<owner>/<repo>` and a token in
// GITHUB_ACTOR and GITHUB_TOKEN. `embedpdf-bindings` is versioned by the
// engine pin in gradle.properties, `0.0.0-engine.<hash>`, so facade and UI
// releases reuse an engine already published; the other four take
// `embedpdf.version`.
class PublishedModule(val artifactId: String, val summary: String)

val publishedModules = mapOf(
    "bindings" to PublishedModule(
        "embedpdf-bindings",
        "The PDF viewer engine for Android: native libraries for arm64-v8a and x86_64 and their generated Kotlin bindings.",
    ),
    "embedpdf" to PublishedModule(
        "embedpdf",
        "A Kotlin facade over the PDF viewer engine: PdfViewer, a typed capability per plugin, and PdfException.",
    ),
    "javascript" to PublishedModule(
        "embedpdf-javascript",
        "PDF JavaScript for the viewer, run in the platform JavaScript sandbox.",
    ),
    "models" to PublishedModule(
        "embedpdf-models",
        "Observable models over the PDF viewer for View-based and Compose apps, with no Compose dependency.",
    ),
    "compose" to PublishedModule(
        "embedpdf-compose",
        "Jetpack Compose views over the PDF viewer models, drawn with Material 3.",
    ),
)

val sourceRepository = "https://github.com/MFB-Technologies-Inc/embed-pdf-viewer"
val engineRelease = providers.gradleProperty("embedpdf.engineRelease").get()
val packageVersion = providers.gradleProperty("embedpdf.version").get()
val packagesRepository = providers.gradleProperty("embedpdf.packagesRepository").orNull
val repoRoot: File = rootDir.resolve("../../..").canonicalFile

subprojects {
    val published = publishedModules[name] ?: return@subprojects
    group = "com.embedpdf"
    version = if (name == "bindings") "0.0.0-engine.${engineRelease.removePrefix("android-engine-")}" else packageVersion
    apply(plugin = "maven-publish")

    pluginManager.withPlugin("com.android.library") {
        extensions.configure<com.android.build.api.dsl.LibraryExtension>("android") {
            publishing {
                singleVariant("release") { withSourcesJar() }
            }
        }
    }

    extensions.configure<PublishingExtension>("publishing") {
        repositories {
            if (packagesRepository != null) {
                maven {
                    name = "GitHubPackages"
                    url = uri("https://maven.pkg.github.com/$packagesRepository")
                    credentials {
                        username = System.getenv("GITHUB_ACTOR")
                        password = System.getenv("GITHUB_TOKEN")
                    }
                }
            }
        }
    }

    // AGP makes the `release` component after the module is evaluated.
    components.matching { it.name == "release" }.all {
        val component = this
        extensions.configure<PublishingExtension>("publishing") {
            publications.register<MavenPublication>("release") {
                from(component)
                artifactId = published.artifactId
                // The Compose artifacts carry no version of their own in
                // the catalog; the published metadata names the versions the
                // BOM resolved, so a consumer without the BOM gets the same.
                versionMapping {
                    usage("java-api") { fromResolutionOf("releaseCompileClasspath") }
                    usage("java-runtime") { fromResolutionOf("releaseRuntimeClasspath") }
                }
                pom {
                    name.set(published.artifactId)
                    description.set(published.summary)
                    url.set(sourceRepository)
                    licenses {
                        license {
                            name.set("Apache-2.0")
                            url.set("$sourceRepository/blob/HEAD/LICENSE")
                        }
                    }
                    scm {
                        url.set(sourceRepository)
                        connection.set("scm:git:$sourceRepository.git")
                    }
                }
            }
        }
    }

    // The engine version names the hash of the tree that built it, so a
    // publish from a tree whose hash differs from the pin would put another
    // engine under that version. android-engine-hash.sh runs only here, at
    // publish time, rather than at every configuration.
    if (name == "bindings") {
        tasks.matching { it.name.startsWith("publish") }.configureEach {
            doFirst {
                val process = ProcessBuilder("bash", repoRoot.resolve("platforms/android/android-engine-hash.sh").path)
                    .redirectError(ProcessBuilder.Redirect.INHERIT)
                    .start()
                val hash = process.inputStream.bufferedReader().readText().trim()
                check(process.waitFor() == 0) { "android-engine-hash.sh failed" }
                check(engineRelease == "android-engine-$hash") {
                    "gradle.properties pins $engineRelease but this tree builds android-engine-$hash; " +
                        "set embedpdf.engineRelease=android-engine-$hash before publishing"
                }
            }
        }
    }
}
