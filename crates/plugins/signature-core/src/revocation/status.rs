//! `revocation/status.ts`: when a revocation answer applies.

use super::answer::AnswerCheck;

/// How long an OCSP answer that names no end to its window speaks for.
const OPEN_WINDOW_MS: f64 = 5.0 * 60.0 * 1000.0;

/// `RevocationStatus['status']`, which is also `RevocationVerdict`.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Status {
    Good,
    Revoked,
    Unknown,
}

impl Status {
    pub fn name(self) -> &'static str {
        match self {
            Status::Good => "good",
            Status::Revoked => "revoked",
            Status::Unknown => "unknown",
        }
    }
}

/// `revocationStatusAt`: what the checked answers about one certificate say
/// at `at` (epoch milliseconds), with the index of the answer that decided
/// it.
pub fn revocation_status_at(checks: &[&AnswerCheck], at: f64) -> (Status, Option<usize>) {
    let mut good = None;
    let mut revoked: Option<(usize, f64)> = None;
    for (index, check) in checks.iter().enumerate() {
        match check {
            AnswerCheck::Nothing { .. } => {}
            AnswerCheck::Revoked { revoked_at, .. } => {
                if *revoked_at <= at {
                    if revoked.is_none_or(|(_, earliest)| *revoked_at < earliest) {
                        revoked = Some((index, *revoked_at));
                    }
                } else if good.is_none() {
                    good = Some(index);
                }
            }
            AnswerCheck::Good(window) => {
                let from = window.this_update;
                let until = window.next_update.unwrap_or(from + OPEN_WINDOW_MS);
                if (from >= at || at <= until) && good.is_none() {
                    good = Some(index);
                }
            }
        }
    }
    if let Some((index, _)) = revoked {
        return (Status::Revoked, Some(index));
    }
    match good {
        Some(index) => (Status::Good, Some(index)),
        None => (Status::Unknown, None),
    }
}
