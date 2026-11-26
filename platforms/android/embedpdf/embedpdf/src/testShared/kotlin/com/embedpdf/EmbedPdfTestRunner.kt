package com.embedpdf

import org.junit.runners.model.FrameworkMethod
import org.robolectric.RobolectricTestRunner
import org.robolectric.internal.bytecode.InstrumentationConfiguration

/**
 * Robolectric's runner, loading the generated bindings and JNA from the test
 * JVM's own class loader rather than the sandbox's.
 *
 * The bindings register their callback tables with the one native library
 * when their classes load. A copy loaded again inside a sandbox registers
 * tables of its own, and a plain test that runs later in the same JVM then has
 * its callbacks answered from the sandbox's empty handle maps, which crashes
 * the process in native code. Shared, the bindings load once per JVM, so
 * Robolectric and plain test classes can share one.
 */
class EmbedPdfTestRunner(testClass: Class<*>) : RobolectricTestRunner(testClass) {
    override fun createClassLoaderConfig(method: FrameworkMethod?): InstrumentationConfiguration =
        InstrumentationConfiguration.Builder(super.createClassLoaderConfig(method))
            .doNotAcquirePackage("uniffi.")
            .doNotAcquirePackage("com.sun.jna.")
            .build()
}
