pluginManagement {
    repositories {
        google()
        mavenCentral()
        gradlePluginPortal()
    }
}
dependencyResolutionManagement {
    repositories {
        google()
        mavenCentral()
    }
}

rootProject.name = "embedpdf"
// bindings: the published AAR, the full viewer host's .so files and their
// generated Kotlin. smoke: JVM tests for the engine-only epdf-uniffi crate.
include(":bindings")
include(":smoke")
