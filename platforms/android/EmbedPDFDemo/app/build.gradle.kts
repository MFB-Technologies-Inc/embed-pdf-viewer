import java.util.Properties

plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.kotlin.compose)
}

val repoRoot: File = rootDir.resolve("../../..").canonicalFile

// The library version the demo asks for: `-Pembedpdf.version`, or the one
// ../embedpdf/gradle.properties names. The included build answers any
// version; Maven Local has to hold this one.
val embedpdfVersion: String = providers.gradleProperty("embedpdf.version").orNull
    ?: Properties().apply { rootDir.resolve("../embedpdf/gradle.properties").inputStream().use(::load) }
        .getProperty("embedpdf.version")

android {
    namespace = "com.embedpdf.demo"
    compileSdk = 36

    defaultConfig {
        applicationId = "com.embedpdf.demo"
        minSdk = 24
        targetSdk = 36
        versionCode = 1
        versionName = "0.0.0"
    }

    buildTypes {
        release {
            // R8 runs over the app and the libraries' consumer rules, which
            // keep what JNA reaches by reflection; android-consumer.sh checks
            // that every uniffi and JNA class survives.
            isMinifyEnabled = true
            isShrinkResources = true
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
            // A demo has no release key; the debug key lets the release APK install.
            signingConfig = signingConfigs.getByName("debug")
        }
    }

    buildFeatures {
        compose = true
    }

    packaging {
        jniLibs {
            // Stored, so the linker maps each library straight out of the APK
            // at a 16 KB boundary.
            useLegacyPackaging = false
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
}

kotlin {
    compilerOptions {
        jvmTarget = org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_17
    }
}

/** Copies the two sample documents the demo bundles as assets. */
abstract class SampleDocuments : DefaultTask() {
    @get:InputFiles
    abstract val documents: ConfigurableFileCollection

    @get:OutputDirectory
    abstract val outputDir: DirectoryProperty

    @TaskAction
    fun copy() {
        val out = outputDir.get().asFile
        out.deleteRecursively()
        out.mkdirs()
        documents.forEach { it.copyTo(out.resolve(it.name)) }
    }
}

val sampleDocuments = tasks.register<SampleDocuments>("sampleDocuments") {
    documents.from(
        repoRoot.resolve("pdfium-multiplatform-prototype/fixtures/report.pdf"),
        repoRoot.resolve("pdfium-multiplatform-prototype/fixtures/encrypted-hello.pdf"),
    )
    outputDir.set(layout.buildDirectory.dir("generated/sampleDocuments"))
}

androidComponents {
    onVariants { variant ->
        variant.sources.assets?.addGeneratedSourceDirectory(sampleDocuments, SampleDocuments::outputDir)
    }
}

dependencies {
    implementation(platform(libs.compose.bom))
    implementation("com.embedpdf:embedpdf-compose:$embedpdfVersion")
    implementation(libs.compose.material3)
    implementation(libs.androidx.activity.compose)
    implementation(libs.androidx.lifecycle.viewmodel.compose)
}
