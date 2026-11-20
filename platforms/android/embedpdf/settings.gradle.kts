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
// generated Kotlin.
include(":bindings")
