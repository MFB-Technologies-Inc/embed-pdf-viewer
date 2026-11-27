#!/usr/bin/env python3
"""2026-11-27 (CU-C8). usage: coverage-rows.py <checkout>
Adds the form plugin's nine new capability members (CU-C5's groups, anchor and selection reads)
to the C#, Kotlin and Swift coverage manifests, each after the last row of its source file, as
the inventory lists them. C# and Kotlin rows map to the members this job adds; the Swift rows
name the uniffi export and are `pending` until CU-C9 maps them (CU-C8-D7)."""
import json, sys
root = sys.argv[1]
CT = "System.Threading.CancellationToken"
NEW = [
    ("crates/plugins/form/src/read/fields.rs", "list_selected_fields", "selected_fields", "GetSelectedFieldsAsync", [CT], "selectedFields"),
    ("crates/plugins/form/src/read/widgets.rs", "get_widget_anchor", "widget_anchor", "GetWidgetAnchorAsync", ["EmbedPDF.AnnotationRef", CT], "widgetAnchor"),
    ("crates/plugins/form/src/read/widgets.rs", "is_widget_focused", "is_widget_focused", "IsWidgetFocusedAsync", ["EmbedPDF.AnnotationRef", CT], "isWidgetFocused"),
    ("crates/plugins/form/src/services/groups.rs", "list_groups", "groups", "GetGroupsAsync", [CT], "groups"),
    ("crates/plugins/form/src/services/groups.rs", "can_set_group", "can_set_group", "CanSetGroupAsync", ["System.String", CT], "canSetGroup"),
    ("crates/plugins/form/src/write/fields.rs", "move_to_group", "move_to_group", "MoveToGroupAsync", ["System.Collections.Generic.IEnumerable<EmbedPDF.FormFieldRef>", "System.String", CT], "moveToGroup"),
    ("crates/plugins/form/src/write/groups.rs", "set_groups", "set_groups", "SetGroupsAsync", ["System.Collections.Generic.IEnumerable<EmbedPDF.FormGroupInput>", CT], "setGroups"),
    ("crates/plugins/form/src/write/groups.rs", "get_design_group", "design_group", "GetDesignGroupAsync", [CT], "designGroup"),
    ("crates/plugins/form/src/write/groups.rs", "set_design_group", "set_design_group", "SetDesignGroupAsync", ["System.String", CT], "setDesignGroup"),
]
KOTLIN = "platforms/android/embedpdf/embedpdf/src/main/kotlin/com/embedpdf/Form.kt"

def rows(kind):
    for path, symbol, export, cs, params, kt in NEW:
        row = {"crate": "form", "path": path, "owner": "Core", "symbol": symbol}
        if kind == "csharp":
            row["export"] = {"path": "crates/plugins/form/src/ffi.rs", "symbol": export}
            row["csharp"] = {"type": "EmbedPDF.FormCapability", "member": cs, "parameters": params}
        elif kind == "kotlin":
            row["kotlin"] = {"path": KOTLIN, "symbol": kt}
        else:
            row["export"] = {"path": "crates/plugins/form/src/ffi.rs", "symbol": export}
            row["pending"] = "CU-C9"
        yield row

def add(path, kind):
    path = f"{root}/{path}"
    manifest = json.load(open(path))
    members = manifest["members"]
    have = {(m["path"], m["owner"], m["symbol"]) for m in members}
    added = 0
    for row in rows(kind):
        if (row["path"], row["owner"], row["symbol"]) in have:
            continue
        same = [i for i, m in enumerate(members) if m["path"] == row["path"]]
        form = [i for i, m in enumerate(members) if m["crate"] == "form" and m["path"] < row["path"]]
        at = (same or form or [len(members) - 1])[-1] + 1
        members.insert(at, row)
        added += 1
    open(path, "w").write(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n")
    print(path, "added", added)

add("platforms/dotnet/csharp-api-coverage.json", "csharp")
add("platforms/android/kotlin-api-coverage.json", "kotlin")
add("platforms/apple/swift-api-coverage.json", "swift")
