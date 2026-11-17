//! The engine's own thread. PDFium never leaves `OwnerThread`; this client
//! thread queues calls to it so the kernel thread never blocks on a render.
//! A call cancelled while it waits in line never runs. Once it runs, a read
//! gets its token, which the host checks between units of work; a single
//! PDFium call cannot be interrupted, so a read cancelled past its last
//! check completes. A write runs to its end however it is cancelled, as
//! `WorkerHost` runs one in one go, deaf to an abort until it returns: its
//! document is never left with part of it. Either way the kernel, which
//! settled the call at the abort, drops the reply.

use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::mpsc::{self, Sender};
use std::sync::{Arc, Mutex, Weak};
use std::thread::JoinHandle;

use epdf_engine::cancel::CancelToken;
use epdf_engine::dispatch::OwnerThread;
use epdf_engine::error::{EngineError, EngineErrorCode};
use epdf_engine::host::{Host, HostResponse};

type Reply = Box<dyn FnOnce(HostResponse) + Send>;

/// A wire kind, and what to run as the next call of it starts.
#[cfg(test)]
type OnStart = (&'static str, Box<dyn FnOnce() + Send>);

enum Request {
    Call {
        json: String,
        payloads: Vec<Vec<u8>>,
        cancel: CancelToken,
        /// Whether the host may see `cancel` while the call runs: false for
        /// a write, which runs to its end once started.
        stops_mid_call: bool,
        reply: Reply,
    },
    /// Answered once every request queued before it has been answered.
    Drained(Box<dyn FnOnce() + Send>),
}

struct State {
    tx: Option<Sender<Request>>,
    handle: Option<JoinHandle<()>>,
}

#[derive(Clone)]
pub struct EngineWorker {
    state: Arc<Mutex<State>>,
    /// Set by `join`: what is still queued is answered without PDFium.
    closing: Arc<AtomicBool>,
    /// Held by the client thread until PDFium is closed: a test's view of
    /// whether the thread outlived its host.
    alive: Weak<()>,
    /// Run on the client thread as the next call of a kind starts, before
    /// the host takes it: a test's way to cancel a call that is running.
    #[cfg(test)]
    on_start: Arc<Mutex<Option<OnStart>>>,
}

impl EngineWorker {
    pub fn spawn() -> EngineWorker {
        EngineWorker::spawn_with(Host::new)
    }

    /// `spawn`, with the host built by `setup` on the owner thread: the
    /// stamp plugin's asset engine installs an image encoder there.
    pub fn spawn_with(setup: impl FnOnce() -> Host + Send + 'static) -> EngineWorker {
        let (tx, rx) = mpsc::channel::<Request>();
        let alive = Arc::new(());
        let witness = Arc::downgrade(&alive);
        let closing = Arc::new(AtomicBool::new(false));
        let skip = closing.clone();
        #[cfg(test)]
        let on_start: Arc<Mutex<Option<OnStart>>> = Arc::default();
        #[cfg(test)]
        let starting = on_start.clone();
        let handle = std::thread::Builder::new()
            .name("epdf-engine-client".into())
            .spawn(move || {
                let mut owner = OwnerThread::start(setup);
                for request in rx {
                    match request {
                        // Its canceller has answered it.
                        Request::Call { cancel, .. } if cancel.is_cancelled() => {}
                        Request::Call { reply, .. } if skip.load(Ordering::SeqCst) => {
                            reply(shut_down());
                        }
                        Request::Call { json, payloads, cancel, stops_mid_call, reply } => {
                            #[cfg(test)]
                            {
                                let mut starting = starting.lock().unwrap();
                                let kind = starting.as_ref().map(|(kind, _)| *kind);
                                if kind.is_some_and(|kind| {
                                    json.contains(&format!(r#""kind":"{kind}""#))
                                }) && let Some((_, start)) = starting.take()
                                {
                                    drop(starting);
                                    start();
                                }
                            }
                            let cancel = if stops_mid_call { cancel } else { CancelToken::never() };
                            let response = owner
                                .run(move |host: &mut Host| {
                                    host.call_with_cancel(&json, payloads, &cancel)
                                })
                                .unwrap_or_else(|error| HostResponse {
                                    json: serde_json::json!({ "error": error }).to_string(),
                                    payloads: Vec::new(),
                                });
                            reply(response);
                        }
                        Request::Drained(reply) => reply(),
                    }
                }
                owner.close();
                drop(alive);
            })
            .expect("the engine client thread");
        EngineWorker {
            state: Arc::new(Mutex::new(State { tx: Some(tx), handle: Some(handle) })),
            closing,
            alive: witness,
            #[cfg(test)]
            on_start,
        }
    }

    /// Queue a call. After `shutdown` the reply still comes, at once and as
    /// an error, so a caller waiting on it is never left pending. Cancelling
    /// `cancel` before the call runs drops it unanswered. Once it runs, the
    /// host checks `cancel` between units of work only if `stops_mid_call`;
    /// a write passes false and runs to its end.
    pub fn submit(
        &self,
        json: String,
        payloads: Vec<Vec<u8>>,
        cancel: CancelToken,
        stops_mid_call: bool,
        reply: Reply,
    ) {
        let request = Request::Call { json, payloads, cancel, stops_mid_call, reply };
        if let Some(Request::Call { reply, .. }) = self.send(request) {
            reply(shut_down());
        }
    }

    /// Refuse further calls. `drained` runs, on the client thread, once
    /// every call queued before this one has been answered; at once, on
    /// this thread, if the worker was already shut down.
    pub fn shutdown_after(&self, drained: Box<dyn FnOnce() + Send>) {
        if let Some(Request::Drained(drained)) = self.send(Request::Drained(drained)) {
            drained();
        }
        self.shutdown();
    }

    /// Refuse further calls. Calls already queued still run.
    pub fn shutdown(&self) {
        self.state.lock().unwrap().tx.take();
    }

    /// `shutdown`, then wait for the client thread to answer what is queued
    /// (as shut down, without running it), close PDFium and exit. A call
    /// already inside PDFium finishes first: it cannot be interrupted. Never called on the client thread: only the
    /// kernel thread owns a worker, and the client thread's replies only
    /// post to the kernel thread (a post that never blocks, and fails once
    /// the kernel thread is stopping).
    pub fn join(&self) {
        self.closing.store(true, Ordering::SeqCst);
        let handle = {
            let mut state = self.state.lock().unwrap();
            state.tx.take();
            state.handle.take()
        };
        if let Some(handle) = handle {
            let _ = handle.join();
        }
    }

    #[doc(hidden)]
    pub fn alive(&self) -> Weak<()> {
        self.alive.clone()
    }

    /// Keep the client thread from taking the calls queued after this one
    /// until `gate` receives: a test's way to cancel a call while it waits.
    #[cfg(test)]
    pub(crate) fn hold(&self, gate: mpsc::Receiver<()>) {
        let held = self.send(Request::Drained(Box::new(move || {
            let _ = gate.recv();
        })));
        assert!(held.is_none(), "the worker takes requests");
    }

    /// Run `start` on the client thread as the next call of `kind` starts,
    /// before the host takes it.
    #[cfg(test)]
    pub(crate) fn on_next_start(&self, kind: &'static str, start: Box<dyn FnOnce() + Send>) {
        *self.on_start.lock().unwrap() = Some((kind, start));
    }

    /// The request back if the thread no longer takes requests.
    fn send(&self, request: Request) -> Option<Request> {
        match self.state.lock().unwrap().tx.as_ref() {
            Some(tx) => tx.send(request).err().map(|e| e.0),
            None => Some(request),
        }
    }
}

fn shut_down() -> HostResponse {
    HostResponse {
        json: serde_json::json!({
            "error": EngineError::with_message(EngineErrorCode::DocNotOpen, "the engine is shut down")
        })
        .to_string(),
        payloads: Vec::new(),
    }
}
