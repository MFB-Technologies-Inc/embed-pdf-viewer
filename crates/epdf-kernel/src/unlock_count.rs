//! The document handle a session keeps and hands out: the engine's handle
//! with each successful `security.unlock` counted. An owner-password unlock
//! upgrades an open document's rights with no event and no store write, so
//! a memo of permission answers keys on this count (`ControllerContext`'s
//! permission memo). Every handle the kernel hands out for the session,
//! guarded or not, is this one or wraps it, so each unlock counts once.

use std::cell::Cell;
use std::rc::Rc;

use serde_json::Value;

use crate::engine::{
    Answer, CallFacts, DocumentHandle, DocumentSecurity, EngineCall, ObjectNumbersLost,
};
use crate::error::Error;
use crate::store::Unsubscribe;

/// `handle`, counting each unlock through it that resolves into `unlocks`.
pub(crate) fn counting_unlocks(
    handle: Rc<dyn DocumentHandle>,
    unlocks: Rc<Cell<u64>>,
) -> Rc<dyn DocumentHandle> {
    Rc::new(CountingHandle { inner: handle, unlocks })
}

struct CountingHandle {
    inner: Rc<dyn DocumentHandle>,
    unlocks: Rc<Cell<u64>>,
}

impl DocumentHandle for CountingHandle {
    fn is_local(&self) -> bool {
        self.inner.is_local()
    }
    fn held_object_numbers(&self) -> usize {
        self.inner.held_object_numbers()
    }
    fn take_object_number(&self) -> Result<Option<u32>, Error> {
        self.inner.take_object_number()
    }
    fn on_lost_object_numbers(&self, listener: Rc<dyn Fn(&ObjectNumbersLost)>) -> Unsubscribe {
        self.inner.on_lost_object_numbers(listener)
    }
    fn reserve_object_numbers(&self, count: u32) -> EngineCall<()> {
        self.inner.reserve_object_numbers(count)
    }
    fn reserve_object_number_count(&self, count: f64) -> EngineCall<()> {
        self.inner.reserve_object_number_count(count)
    }
    fn id(&self) -> String {
        self.inner.id()
    }
    fn capabilities(&self) -> Value {
        self.inner.capabilities()
    }
    fn security(&self) -> Rc<dyn DocumentSecurity> {
        Rc::new(CountingSecurity { inner: self.inner.security(), unlocks: self.unlocks.clone() })
    }
    fn has(&self, member: &str) -> bool {
        self.inner.has(member)
    }
    fn call(&self, path: &str, pon: Option<u32>, args: Vec<Value>) -> EngineCall<Value> {
        self.inner.call(path, pon, args)
    }
    fn call_with(
        &self,
        facts: &CallFacts,
        path: &str,
        pon: Option<u32>,
        args: Vec<Value>,
    ) -> EngineCall<Value> {
        self.inner.call_with(facts, path, pon, args)
    }
    fn call_answer(&self, path: &str, pon: Option<u32>, args: Vec<Value>) -> EngineCall<Answer> {
        self.inner.call_answer(path, pon, args)
    }
    fn call_answer_with(
        &self,
        facts: &CallFacts,
        path: &str,
        pon: Option<u32>,
        args: Vec<Value>,
    ) -> EngineCall<Answer> {
        self.inner.call_answer_with(facts, path, pon, args)
    }
    fn page_index(&self, pon: u32) -> Result<u32, Error> {
        self.inner.page_index(pon)
    }
    fn subscribe(&self, listener: Rc<dyn Fn(&Value)>) -> Unsubscribe {
        self.inner.subscribe(listener)
    }
    fn last_server_sequence(&self) -> Option<String> {
        self.inner.last_server_sequence()
    }
    fn set_working_set(&self, view: &str, pages: Value) {
        self.inner.set_working_set(view, pages)
    }
}

struct CountingSecurity {
    inner: Rc<dyn DocumentSecurity>,
    unlocks: Rc<Cell<u64>>,
}

impl DocumentSecurity for CountingSecurity {
    fn allows(&self, capability: &str) -> bool {
        self.inner.allows(capability)
    }
    fn allows_field(&self, action: &str, target: &Value) -> bool {
        self.inner.allows_field(action, target)
    }
    fn allows_annotation(&self, action: &str, target: &Value) -> bool {
        self.inner.allows_annotation(action, target)
    }
    fn allows_annotation_create(&self) -> bool {
        self.inner.allows_annotation_create()
    }
    fn try_allows(&self, capability: &str) -> Option<bool> {
        self.inner.try_allows(capability)
    }
    fn try_allows_field(&self, action: &str, target: &Value) -> Option<bool> {
        self.inner.try_allows_field(action, target)
    }
    fn try_allows_annotation_create(&self) -> Option<bool> {
        self.inner.try_allows_annotation_create()
    }
    fn allows_annotation_mutation(&self, action: &str, target: &Value) -> bool {
        self.inner.allows_annotation_mutation(action, target)
    }
    fn allows_annotation_group_assignment(&self, group_id: &str) -> bool {
        self.inner.allows_annotation_group_assignment(group_id)
    }
    fn effective_scope(&self) -> Vec<String> {
        self.inner.effective_scope()
    }
    fn identity(&self) -> Value {
        self.inner.identity()
    }
    fn current(&self) -> Value {
        self.inner.current()
    }
    fn password_prompt(&self) -> Value {
        self.inner.password_prompt()
    }
    /// Counted at the moment the engine's answer settles, before anything
    /// awaiting it resumes; observing adds no turn.
    fn unlock(&self, input: Value) -> EngineCall<Value> {
        let pending = self.inner.unlock(input);
        let unlocks = self.unlocks.clone();
        pending.on_settle(move |result| {
            if result.is_ok() {
                unlocks.set(unlocks.get() + 1);
            }
        });
        pending
    }
}
