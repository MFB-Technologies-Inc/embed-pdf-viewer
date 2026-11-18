//! `revocation/http-source.ts`: a revocation source that asks through the
//! app's own route (D-2), over the host's transport (D-1).

use std::rc::Rc;

use base64::Engine as _;
use epdf_kernel::Error;
use epdf_kernel::rt::Promise;
use serde_json::{Map, Value, json};

use super::source::{RevocationRequest, RevocationSource};
use crate::transport::{
    DEFAULT_TIMEOUT_MS, HttpTransport, MAX_REVOCATION_REPLY_BYTES, TransportHeader,
    TransportRequest, post_with_retries,
};

/// `HttpRevocationSourceOptions`.
#[derive(Clone, Debug, PartialEq)]
pub struct HttpRevocationSourceOptions {
    /// The route's address.
    pub url: String,
    pub headers: Vec<TransportHeader>,
    /// Default 10000.
    pub timeout_ms: Option<u32>,
    /// Further attempts when no answer came. Default 1.
    pub retries: Option<u32>,
}

/// `RevocationRouteRequest`: what the route is sent, as JSON. `body` is an
/// OCSP question's request in base64.
pub fn route_request(request: &RevocationRequest) -> Value {
    let mut out = Map::new();
    out.insert("kind".into(), json!(request.kind()));
    out.insert("url".into(), json!(request.url()));
    if let RevocationRequest::Ocsp { body, .. } = request {
        out.insert("body".into(), json!(base64::engine::general_purpose::STANDARD.encode(body)));
    }
    Value::Object(out)
}

/// `refusalOf`: `{ reason, message }` when the reply is the route's JSON.
fn refusal_of(body: &[u8]) -> (Option<String>, Option<String>) {
    let said: Value = serde_json::from_slice(body).unwrap_or(Value::Null);
    let text = |key: &str| said.get(key).and_then(Value::as_str).map(str::to_string);
    (text("reason"), text("message"))
}

struct HttpRevocationSource {
    transport: Rc<dyn HttpTransport>,
    options: HttpRevocationSourceOptions,
}

/// `httpRevocationSource`: asks through the route, and tries again only when
/// no answer came. A `Network` error when none came, `SignatureRefused` when
/// the route refused; each carries `details.source` and `details.route`.
pub fn http_revocation_source(
    transport: Rc<dyn HttpTransport>,
    options: HttpRevocationSourceOptions,
) -> Rc<dyn RevocationSource> {
    Rc::new(HttpRevocationSource { transport, options })
}

impl RevocationSource for HttpRevocationSource {
    fn fetch(&self, request: RevocationRequest) -> Promise<Vec<u8>> {
        let transport = self.transport.clone();
        let options = self.options.clone();
        Promise::spawn(async move {
            let route = options.url.clone();
            let source = request.url().to_string();
            let timeout_ms = options.timeout_ms.unwrap_or(DEFAULT_TIMEOUT_MS);
            let details = |more: Value| {
                let mut out = json!({ "source": source, "route": route });
                if let (Some(out), Some(more)) = (out.as_object_mut(), more.as_object()) {
                    out.extend(more.clone());
                }
                out
            };
            let post = TransportRequest {
                url: route.clone(),
                content_type: "application/json".into(),
                headers: options.headers.clone(),
                body: serde_json::to_vec(&route_request(&request)).unwrap_or_default(),
                timeout_ms,
                max_reply_bytes: MAX_REVOCATION_REPLY_BYTES,
            };
            let exchanged = post_with_retries(
                &transport,
                &post,
                options.retries.unwrap_or(1),
                |status, body| {
                    let said = refusal_of(body).1.map(|m| format!(": {m}")).unwrap_or_default();
                    format!("HTTP {status}{said}")
                },
            )
            .await;
            let (status, body) = exchanged.map_err(|why| {
                Error::engine("Network", format!("{source} did not answer through {route}: {why}"))
                    .with("details", details(json!({})))
            })?;
            if !(200..300).contains(&status) {
                let (reason, message) = refusal_of(&body);
                let mut more = json!({ "httpStatus": status });
                if let Some(reason) = &reason {
                    more["reason"] = json!(reason);
                }
                return Err(Error::engine(
                    "SignatureRefused",
                    format!(
                        "the route at {route} refused to ask {source}: {}",
                        message.unwrap_or_else(|| format!("HTTP {status}"))
                    ),
                )
                .with("details", details(more)));
            }
            Ok(body)
        })
    }
}
