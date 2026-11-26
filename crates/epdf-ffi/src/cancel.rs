//! Cancelling a call from the foreign side.
//!
//! UniFFI 0.32's Swift does not cancel the Rust future when the awaiting
//! Swift task is cancelled, so dropping the future (which aborts a
//! `call_promise` promise) never happens on cancellation. A `CancelToken` is
//! the explicit path instead: the foreign wrapper makes one per call, passes
//! it in, and cancels it from its cancellation handler.

use std::sync::{Arc, Mutex};

use epdf_kernel::signal::AbortSignal;
use epdf_kernel::{Error, Promise};

type Hook = Box<dyn FnOnce() + Send>;

#[derive(Default)]
struct State {
    cancelled: bool,
    hook: Option<Hook>,
}

/// Governs one call. Cancelling is idempotent, and cancelling after the call
/// finished does nothing.
#[derive(uniffi::Object)]
pub struct CancelToken {
    state: Mutex<State>,
}

#[uniffi::export]
impl CancelToken {
    #[uniffi::constructor]
    pub fn new() -> Arc<Self> {
        Arc::new(CancelToken { state: Mutex::new(State::default()) })
    }

    pub fn cancel(&self) {
        let hook = {
            let mut state = self.state.lock().unwrap();
            if state.cancelled {
                return;
            }
            state.cancelled = true;
            state.hook.take()
        };
        if let Some(hook) = hook {
            hook();
        }
    }

    pub fn is_cancelled(&self) -> bool {
        self.state.lock().unwrap().cancelled
    }
}

impl CancelToken {
    /// Run `hook` when the token is cancelled, or now if it already was. A
    /// token governs one call, so a second hook replaces the first.
    pub fn on_cancel(&self, hook: impl FnOnce() + Send + 'static) {
        let mut state = self.state.lock().unwrap();
        if state.cancelled {
            drop(state);
            hook();
        } else {
            state.hook = Some(Box::new(hook));
        }
    }
}

/// Start a plugin call that takes an `AbortSignal` in its options, and
/// abort that signal when the call's promise is aborted: by a `CancelToken`
/// through [`KernelBound::call_promise_cancellable`](crate::KernelBound), or
/// by a dropped future. The plugin decides what a cancel does; the answer is
/// whatever its promise settles with.
pub fn signalled<T: Clone + 'static>(
    run: impl FnOnce(AbortSignal) -> Result<Promise<T>, Error>,
) -> Result<Promise<T>, Error> {
    let signal = AbortSignal::new();
    let task = run(signal.clone())?;
    Ok(task.with_abort(move |reason| signal.abort(Error::abort(reason))))
}

/// [`signalled`], but the answer rejects `operation-cancelled` for
/// `capability` as soon as the signal aborts, even when the plugin's promise
/// settles later (a change the engine already admitted still reconciles).
pub fn signalled_cancellable<T: Clone + 'static>(
    capability: &str,
    run: impl FnOnce(AbortSignal) -> Result<Promise<T>, Error>,
) -> Result<Promise<T>, Error> {
    let signal = AbortSignal::new();
    let task = run(signal.clone())?;
    Ok(epdf_kernel::cancellable::cancellable(capability, Some(signal.clone()), task)
        .with_abort_member(move |reason| signal.abort(Error::abort(reason))))
}

#[cfg(test)]
mod tests {
    use std::cell::RefCell;
    use std::rc::Rc;

    use super::*;

    /// A pending promise, and the signal `run` was handed.
    fn started(
        wrap: impl FnOnce(
            Box<dyn FnOnce(AbortSignal) -> Result<Promise<u32>, Error>>,
        ) -> Result<Promise<u32>, Error>,
    ) -> (Promise<u32>, AbortSignal) {
        let seen: Rc<RefCell<Option<AbortSignal>>> = Rc::default();
        let keep = seen.clone();
        let promise = wrap(Box::new(move |signal| {
            *keep.borrow_mut() = Some(signal);
            Ok(Promise::<u32>::pending().0)
        }))
        .unwrap();
        let signal = seen.borrow_mut().take().unwrap();
        (promise, signal)
    }

    #[test]
    fn aborting_a_signalled_promise_aborts_the_signal_it_handed_out() {
        let (promise, signal) = started(signalled);
        assert!(!signal.aborted());
        promise.abort(None);
        assert!(signal.aborted());
    }

    #[test]
    fn aborting_a_cancellable_one_aborts_the_signal_and_rejects_cancelled() {
        let (promise, signal) = started(|run| signalled_cancellable("test", run));
        promise.abort(None);
        epdf_kernel::rt::run_until_stalled();
        assert!(signal.aborted());
        let error = promise.peek().expect("settled").unwrap_err();
        assert!(error.is_plugin_error(Some("operation-cancelled")), "{error:?}");
    }

    #[test]
    fn a_run_that_fails_hands_back_its_error() {
        let failed = signalled::<u32>(|_| Err(Error::new("refused")));
        assert!(failed.is_err());
    }
}
