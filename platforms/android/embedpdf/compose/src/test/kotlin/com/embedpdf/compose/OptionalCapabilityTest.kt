package com.embedpdf.compose

import com.embedpdf.PdfException
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.fail
import org.junit.Test

class OptionalCapabilityTest {
    @Test
    fun anUnsupportedPluginIsAbsent() = runBlocking {
        assertNull(optionalCapability<Unit> { throw PdfException(PdfException.Code.UNSUPPORTED, "left out") })
    }

    @Test
    fun anyOtherFailureIsThrown() = runBlocking {
        try {
            optionalCapability<Unit> { throw PdfException(PdfException.Code.NOT_FOUND, "closed") }
            fail("a not-found failure read as a missing plugin")
        } catch (e: PdfException) {
            assertEquals(PdfException.Code.NOT_FOUND, e.code)
        }
    }
}
