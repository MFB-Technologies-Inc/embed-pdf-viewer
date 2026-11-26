package com.embedpdf.compose

import com.embedpdf.models.StageReaderModel
import org.junit.Assert.assertEquals
import org.junit.Test

/** `ImportTests`: the compose module exposes the models, so a consumer of it alone names them. */
class ImportTest {
    @Test
    fun theComposeModuleExposesTheModels() {
        assertEquals("com.embedpdf.models.StageReaderModel", StageReaderModel::class.qualifiedName)
    }
}
