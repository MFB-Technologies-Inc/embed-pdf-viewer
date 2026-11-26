plugins {
    // As in ../embedpdf/build.gradle.kts: AGP 9 compiles Kotlin itself, and
    // declaring the Kotlin plugin without applying it raises the compiler to
    // the catalog's version.
    alias(libs.plugins.android.application) apply false
    alias(libs.plugins.kotlin.android) apply false
    alias(libs.plugins.kotlin.compose) apply false
}
