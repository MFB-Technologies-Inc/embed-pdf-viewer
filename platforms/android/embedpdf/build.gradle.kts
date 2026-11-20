plugins {
    // AGP 9 bundles Kotlin support, so applying org.jetbrains.kotlin.android on
    // top of it fails: both register a `kotlin` extension. Declaring it here
    // without applying it only puts that Kotlin Gradle plugin on the build
    // classpath, which is how the compiler version is raised past AGP's own.
    alias(libs.plugins.android.library) apply false
    alias(libs.plugins.kotlin.android) apply false
    alias(libs.plugins.kotlin.jvm) apply false
    // Puts the API dump tasks `embedpdf/build.gradle.kts` registers on the
    // classpath; its `apiValidation` extension holds their settings.
    alias(libs.plugins.binary.compatibility.validator)
}

apiValidation {
    // bindings is generated and versioned by engine hash; smoke is a test.
    ignoredProjects += listOf("bindings", "smoke")
}
