// The demo is a build of its own, not a module of the library build, so
// `./gradlew check` over the libraries never configures or compiles an app.
//
// By default it includes ../embedpdf and substitutes the five `com.embedpdf`
// coordinates with those projects, so a library change shows up in the next
// demo build. With `-Pembedpdf.fromMavenLocal` it resolves them from Maven
// Local instead, as an app resolves a published release;
// platforms/android/android-consumer.sh builds it that way. With
// `-Pembedpdf.mavenRepository=<dir>` it resolves them from that directory, a
// Maven repository such as the unzipped release asset
// embedpdf-android-maven-<version>.zip; a relative path is taken from this
// directory. release-android.yml builds it that way from the zip it releases.
pluginManagement {
    repositories {
        google()
        mavenCentral()
        gradlePluginPortal()
    }
}

val fromMavenLocal = providers.gradleProperty("embedpdf.fromMavenLocal").isPresent
val mavenRepository = providers.gradleProperty("embedpdf.mavenRepository").orNull

dependencyResolutionManagement {
    repositories {
        if (fromMavenLocal) {
            mavenLocal {
                content { includeGroup("com.embedpdf") }
            }
        }
        if (mavenRepository != null) {
            maven {
                url = uri(settingsDir.resolve(mavenRepository))
                content { includeGroup("com.embedpdf") }
            }
        }
        google()
        mavenCentral()
    }
    versionCatalogs {
        // One catalog for the libraries and the demo, so both build against the same versions.
        create("libs") { from(files("../embedpdf/gradle/libs.versions.toml")) }
    }
}

rootProject.name = "EmbedPDFDemo"
include(":app")

if (!fromMavenLocal && mavenRepository == null) {
    includeBuild("../embedpdf") {
        dependencySubstitution {
            substitute(module("com.embedpdf:embedpdf-bindings")).using(project(":bindings"))
            substitute(module("com.embedpdf:embedpdf")).using(project(":embedpdf"))
            substitute(module("com.embedpdf:embedpdf-javascript")).using(project(":javascript"))
            substitute(module("com.embedpdf:embedpdf-models")).using(project(":models"))
            substitute(module("com.embedpdf:embedpdf-compose")).using(project(":compose"))
        }
    }
}
