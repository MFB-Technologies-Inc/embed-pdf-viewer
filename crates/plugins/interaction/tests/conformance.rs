//! `vectors/plugins/interaction*.json`, generated from `packages/plugin/interaction`.

use epdf_plugin_interaction::harness::{Interaction, functions};
use plugin_conformance::{Registry, assert_suite};

#[test]
fn interaction_matches_the_typescript() {
    assert_suite("plugins/interaction.json", &Registry::new().with(Interaction), &Vec::new());
}

#[test]
fn interaction_functions_match_the_typescript() {
    assert_suite("plugins/interaction-functions.json", &Registry::new(), &functions());
}

/// A live host's dispatch goes out as the typed sample: the vector-only
/// spelling (the sample's own JSON, members in its order and members the
/// contract doesn't name) is for printing a vector's trace, and a live
/// harness keeps none.
#[test]
fn a_live_dispatch_hands_handlers_the_typed_sample() {
    use std::any::Any;
    use std::cell::RefCell;
    use std::collections::HashMap;
    use std::rc::Rc;

    use epdf_kernel::kernel::OpenSource;
    use epdf_kernel::{Kernel, KernelOptions, OpenOptions, rt};
    use epdf_plugin_interaction::{INTERACTION, InteractionCapability};
    use plugin_conformance::fake_engine::{AsEngine, FakeEngine};
    use plugin_conformance::{Answer, Harness, HarnessPlugin, LiveFn};
    use serde_json::{Value, json};

    rt::reset();
    let hovers: Rc<RefCell<Vec<Value>>> = Rc::default();
    let heard = hovers.clone();
    let live: LiveFn = Rc::new(move |spec, args| {
        if spec["$fn"] == "js:hover" {
            heard.borrow_mut().push(args[0].clone());
        }
        Answer::Returns(json!(true))
    });
    let h = Harness::live(vec![Rc::new(Interaction)], HashMap::new(), live);
    let plugin = Interaction.build("interaction", &json!({}), &h).unwrap();
    let page = json!({
        "index": 0, "ref": { "kind": "objectNumber", "objectNumber": 1 }, "label": null,
        "size": { "width": 600, "height": 800 }, "rotation": 0, "userUnit": 1
    });
    let fake = FakeEngine::new(json!({ "documents": { "a": { "pages": [page] } } }));
    let kernel =
        Kernel::new(Rc::new(AsEngine(fake)), vec![plugin], KernelOptions::default()).unwrap();
    let _ = kernel.start();
    rt::run_until_stalled();
    let _ = kernel.documents().open(
        OpenSource::Input(json!({ "kind": "bytes", "id": "a", "bytes": { "$bytes": "" } })),
        OpenOptions::default(),
    );
    rt::run_until_stalled();
    let hub: InteractionCapability = kernel.capability(&INTERACTION, Some("a")).unwrap();
    let cap: Rc<dyn Any> = Rc::new(hub);
    let handler = json!({
        "id": "h", "priority": 1,
        "enabledFor": { "$fn": "js:enabled" },
        "onHover": { "$fn": "js:hover" },
    });
    let _ = Interaction.call(&cap, "registerHandler", vec![handler], &h);
    let sample = json!({
        "extra": 1,
        "page": { "point": { "y": 4, "x": 3 }, "ref": { "kind": "objectNumber", "objectNumber": 1 } },
        "modifiers": { "meta": false, "ctrl": false, "alt": false, "shift": false },
        "viewport": { "y": 2, "x": 1 },
        "phase": "move",
    });
    let _ = Interaction.call(&cap, "dispatchPointer", vec![sample], &h);

    let typed = json!({
        "phase": "move",
        "viewport": { "x": 1.0, "y": 2.0 },
        "modifiers": { "shift": false, "alt": false, "ctrl": false, "meta": false },
        "page": { "ref": { "kind": "objectNumber", "objectNumber": 1 }, "point": { "x": 3.0, "y": 4.0 } },
    });
    let hovers = hovers.borrow();
    assert_eq!(hovers.len(), 1);
    assert_eq!(serde_json::to_string(&hovers[0]).unwrap(), serde_json::to_string(&typed).unwrap());
}
