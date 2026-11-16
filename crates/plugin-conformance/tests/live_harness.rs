//! A harness whose callbacks call a live `LiveFn` instead of replaying a
//! scripted `$fn` spec.

use std::cell::{Cell, RefCell};
use std::collections::HashMap;
use std::rc::Rc;

use plugin_conformance::{Answer, Harness, LiveFn, Outcome, TextMemo};
use serde::ser::{Serialize, SerializeStruct, Serializer};
use serde_json::{Value, json};

type Recorded = Rc<RefCell<Vec<(Value, Vec<Value>)>>>;

#[test]
fn a_callback_forwards_to_the_live_fn() {
    let calls: Recorded = Rc::default();
    let recorded = calls.clone();
    let live: LiveFn = Rc::new(move |spec, args| {
        recorded.borrow_mut().push((spec.clone(), args));
        Answer::Returns(json!(42))
    });
    let harness = Harness::live(Vec::new(), HashMap::new(), live);

    let callback = harness.callback(&json!({ "$fn": "js:7" }));
    let answer = callback.call(vec![json!("a")]);

    match answer {
        Answer::Returns(value) => assert_eq!(value, json!(42)),
        _ => panic!("expected Answer::Returns"),
    }
    let calls = calls.borrow();
    assert_eq!(calls.len(), 1);
    assert_eq!(calls[0].0["$fn"], json!("js:7"));
    assert_eq!(calls[0].1, vec![json!("a")]);
}

/// An answer that counts how many times it has been serialized. The `f32`
/// prints differently through a `Value` than straight to text, so it pins
/// the memo to the `Value` route.
struct Counted {
    name: &'static str,
    scale: f32,
    prints: Cell<u32>,
}

impl Counted {
    fn new(name: &'static str, scale: f32) -> Counted {
        Counted { name, scale, prints: Cell::new(0) }
    }
}

impl Serialize for Counted {
    fn serialize<S: Serializer>(&self, serializer: S) -> Result<S::Ok, S::Error> {
        self.prints.set(self.prints.get() + 1);
        let mut s = serializer.serialize_struct("Counted", 2)?;
        s.serialize_field("name", self.name)?;
        s.serialize_field("scale", &self.scale)?;
        s.end()
    }
}

#[test]
fn text_memo_prints_once_per_rc() {
    let memo = TextMemo::default();
    let value = Rc::new(Counted::new("a", 0.1));

    let first = memo.get_or_print(&value);
    let second = memo.get_or_print(&value);

    assert!(Rc::ptr_eq(&first, &second));
    assert_eq!(value.prints.get(), 1);
}

#[test]
fn text_memo_misses_on_new_rc_at_reused_address() {
    let memo = TextMemo::default();
    let old = Rc::new(Counted::new("old", 1.5));
    let old_text = memo.get_or_print(&old);
    let old_addr = Rc::as_ptr(&old);
    drop(old);

    let mut held = Vec::new();
    let mut new = Rc::new(Counted::new("new", 2.5));
    for _ in 0..1_000 {
        if Rc::as_ptr(&new) == old_addr {
            break;
        }
        held.push(new);
        new = Rc::new(Counted::new("new", 2.5));
    }
    let new_text = memo.get_or_print(&new);

    assert_eq!(new.prints.get(), 1);
    assert!(!Rc::ptr_eq(&old_text, &new_text));
    assert_eq!(&*new_text, r#"{"name":"new","scale":2.5}"#);
}

#[test]
fn outcome_text_is_byte_identical() {
    let live: LiveFn = Rc::new(|_, _| Answer::Returns(Value::Null));
    let harness = Harness::live(Vec::new(), HashMap::new(), live);
    let memo = TextMemo::default();
    let value = Rc::new(Counted::new("a", 0.1));

    let text = memo.get_or_print(&value);
    let via_value = serde_json::to_value(&*value).unwrap();

    assert_eq!(&*text, via_value.to_string());
    assert_eq!(
        harness.outcome_text_raw(Outcome::Text(text.clone())),
        harness.outcome_text_raw(Outcome::Returned(via_value.clone())),
    );
    assert_eq!(
        harness.outcome_json_raw(Outcome::Text(text.clone())),
        harness.outcome_json_raw(Outcome::Returned(via_value.clone())),
    );
    assert_eq!(
        harness.outcome_json(Outcome::Text(text)),
        harness.outcome_json(Outcome::Returned(via_value)),
    );
}
