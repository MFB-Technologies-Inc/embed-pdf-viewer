//! The `signature-compat/v4/` corpus (3f54d7016): a new appearance on a signature field nobody
//! signed, saved after the signature. Each fixture is analyzed from its signature to the saved
//! bytes and checked against its entry in `policy-expectations.json` (D-3 of the 2026-11-27b
//! catch-up). The TypeScript has no test that reads these entries; they are the corpus's own
//! record of what the policy says.

use epdf_engine::host::Host;
use serde_json::{Value, json};

mod common;
use common::{sig_call, sig_list, sig_open_fixture};

const COMPAT: &str = "packages/engine/main/test/fixtures/signature-compat";

fn hex(digest: [u8; 32]) -> String {
    digest.iter().map(|b| format!("{b:02x}")).collect()
}

/// The v4 fixtures, by the `id` their file name starts with.
fn v4_fixtures() -> Vec<(String, String)> {
    let dir = test_support::workspace_root().join(format!("{COMPAT}/v4"));
    let mut out: Vec<(String, String)> = std::fs::read_dir(&dir)
        .unwrap()
        .map(|e| e.unwrap().file_name().into_string().unwrap())
        .filter(|n| n.ends_with(".pdf"))
        .map(|n| (n.split('-').next().unwrap().to_string(), n))
        .collect();
    out.sort();
    out
}

#[test]
fn each_v4_fixture_is_judged_as_its_policy_expectation_says() {
    let expectations: Value = serde_json::from_slice(
        &std::fs::read(
            test_support::workspace_root().join(format!("{COMPAT}/policy-expectations.json")),
        )
        .unwrap(),
    )
    .unwrap();
    let entries = expectations["entries"].as_array().unwrap();
    let fixtures = v4_fixtures();
    assert_eq!(fixtures.len(), 11, "{fixtures:?}");
    let mut problems = Vec::new();
    for (id, name) in &fixtures {
        let entry = entries
            .iter()
            .find(|e| e["id"] == json!(id))
            .unwrap_or_else(|| panic!("no expectation for v4/{name}"));
        assert_eq!(entry["policyVersion"], json!(6), "v4/{name}");
        let path = format!("{COMPAT}/v4/{name}");
        let bytes = std::fs::read(test_support::workspace_root().join(&path)).unwrap();
        let mut host = Host::new();
        let (doc, _) = sig_open_fixture(&mut host, &path);
        assert_eq!(
            json!(hex(pdfium::sha256(&bytes).unwrap())),
            entry["sha256"],
            "v4/{name}: not the observed bytes"
        );
        let listed = sig_list(&mut host, json!({ "kind": "signatures.list", "docId": doc }));
        let signed: Vec<u64> = listed["signatures"]
            .as_array()
            .unwrap()
            .iter()
            .filter(|s| s["signed"] == json!(true))
            .map(|s| s["index"].as_u64().unwrap())
            .collect();
        let expected = entry["modifications"].as_array().unwrap();
        assert_eq!(signed.len(), expected.len(), "v4/{name}: {listed}");
        for (index, want) in signed.iter().zip(expected) {
            let analysis = sig_call(
                &mut host,
                json!({ "kind": "signatures.analyze", "docId": doc,
                        "input": { "since": { "signatureIndex": index }, "until": "persisted" } }),
            )["analysis"]
                .take();
            if analysis["policyVersion"] != json!(6) || &analysis["verdict"] != want {
                problems.push(format!(
                    "v4/{name} signature {index} ({}): {} at policy {}, expected {want}; findings {}",
                    entry["relationship"].as_str().unwrap_or(""),
                    analysis["verdict"],
                    analysis["policyVersion"],
                    analysis["current"]["primary"],
                ));
            }
        }
    }
    assert!(problems.is_empty(), "{}", problems.join("\n"));
}
