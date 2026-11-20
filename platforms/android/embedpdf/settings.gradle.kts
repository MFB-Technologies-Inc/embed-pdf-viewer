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
// generated Kotlin. embedpdf: the hand-written facade over it. javascript:
// PDF JavaScript in the platform's sandbox, kept apart so only apps that want
// it take `androidx.javascriptengine`. smoke: JVM tests for the engine-only
// epdf-uniffi crate. models: observable models over the facade, for a
// View-based app or Compose, with no Compose dependency.
include(":bindings")
include(":embedpdf")
include(":javascript")
include(":models")
include(":smoke")
