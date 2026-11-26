package com.embedpdf.demo

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp

/** The document's metadata in three sections, as the metadata plugin reads it. */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun MetadataSheet(sheet: DocumentViewModel.MetadataSheet, onDismiss: () -> Unit) {
    val metadata = sheet.metadata
    val document = listOf(
        R.string.metadata_title to metadata.title,
        R.string.metadata_author to metadata.author,
        R.string.metadata_subject to metadata.subject,
        R.string.metadata_keywords to metadata.keywords,
    )
    val producer = listOf(
        R.string.metadata_creator to metadata.creator,
        R.string.metadata_producer_field to metadata.producer,
        R.string.metadata_created to metadata.createdAt,
        R.string.metadata_modified to metadata.modifiedAt,
    )
    ModalBottomSheet(onDismissRequest = onDismiss) {
        LazyColumn(Modifier.fillMaxWidth().padding(bottom = 16.dp)) {
            item {
                Row(Modifier.fillMaxWidth().padding(horizontal = 16.dp), horizontalArrangement = Arrangement.SpaceBetween) {
                    Text(stringResource(R.string.info), style = MaterialTheme.typography.titleLarge)
                    TextButton(onClick = onDismiss) { Text(stringResource(R.string.done)) }
                }
            }
            item { SectionHeader(R.string.metadata_document) }
            items(document) { (label, value) -> MetadataRow(stringResource(label), value) }
            item { SectionHeader(R.string.metadata_producer) }
            items(producer) { (label, value) -> MetadataRow(stringResource(label), value) }
            if (sheet.custom.isNotEmpty()) {
                item { SectionHeader(R.string.metadata_custom) }
                items(sheet.custom.keys.sorted()) { key -> MetadataRow(key, sheet.custom[key]) }
            }
        }
    }
}

@Composable
private fun SectionHeader(title: Int) {
    Text(
        stringResource(title),
        style = MaterialTheme.typography.labelLarge,
        color = MaterialTheme.colorScheme.primary,
        modifier = Modifier.padding(start = 16.dp, end = 16.dp, top = 16.dp, bottom = 4.dp),
    )
}

/** One label and its value; TalkBack reads them together. */
@Composable
private fun MetadataRow(label: String, value: String?) {
    Row(
        Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 6.dp).semantics(mergeDescendants = true) {},
        horizontalArrangement = Arrangement.spacedBy(16.dp),
    ) {
        Text(label, style = MaterialTheme.typography.bodyLarge)
        Text(
            value ?: stringResource(R.string.metadata_none),
            style = MaterialTheme.typography.bodyLarge,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.weight(1f),
        )
    }
}
