package com.embedpdf

/**
 * Marks API whose shape may change in any release without a deprecation
 * period. A caller opts in with `@OptIn(ExperimentalEmbedPdfApi::class)`.
 */
@RequiresOptIn(
    message = "This EmbedPDF API is experimental and may change without a deprecation period.",
    level = RequiresOptIn.Level.ERROR,
)
@Retention(AnnotationRetention.BINARY)
@Target(AnnotationTarget.CLASS, AnnotationTarget.FUNCTION, AnnotationTarget.PROPERTY, AnnotationTarget.CONSTRUCTOR)
public annotation class ExperimentalEmbedPdfApi
