# JNA maps these by reflection, so R8 has no reference to follow and strips
# them. The failure is at runtime, in a release build only, with an
# UnsatisfiedLinkError that names a class the source obviously contains.
-keep class com.sun.jna.** { *; }
-keep class * implements com.sun.jna.** { *; }

# The generated bindings, for the same reason: every Structure subclass and
# every callback is instantiated by JNA rather than by Kotlin. The full host
# generates one package per component (uniffi.epdf_viewer_host,
# uniffi.epdf_ffi, uniffi.epdf_plugin_*), and a callback interface an app
# implements is called only from native code, so every one is kept.
-keep class uniffi.** { *; }
-keep interface uniffi.** { *; }
-keep class * implements uniffi.** { *; }

# JNA's desktop helpers (Native$AWT) name java.awt classes Android does
# not have. Kept with the rest of JNA, they make R8 stop on missing classes
# in every app that minifies; nothing on Android reaches them.
-dontwarn java.awt.**
