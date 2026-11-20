plugins {
    // AGP 9 bundles Kotlin support, so applying org.jetbrains.kotlin.android on
    // top of it fails: both register a `kotlin` extension. Declaring it here
    // without applying it only puts that Kotlin Gradle plugin on the build
    // classpath, which is how the compiler version is raised past AGP's own.
    alias(libs.plugins.android.library) apply false
    alias(libs.plugins.kotlin.android) apply false
    alias(libs.plugins.kotlin.jvm) apply false
}
