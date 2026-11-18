//! `cms/splice.ts`: editing a CMS in place. An edit copies every byte it
//! doesn't change, and rewrites only the length in front of each element
//! the change sits in. A length written as indefinite stays indefinite.

use crate::parse::ParseError;

/// `BerElement`: one encoded element, by its offsets.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct BerElement {
    pub start: usize,
    pub length_start: usize,
    pub content_start: usize,
    /// For an indefinite length, where its end-of-contents octets begin.
    pub content_end: usize,
    pub end: usize,
    /// Its first identifier octet.
    pub tag: u8,
    pub indefinite: bool,
}

fn malformed() -> ParseError {
    ParseError::Cms { reason: "malformed", message: "CMS is not valid BER".into() }
}

/// `readElement`: the element at `start`, which must end by `limit`.
pub fn read_element(bytes: &[u8], start: usize, limit: usize) -> Result<BerElement, ParseError> {
    let at_byte = |i: usize| bytes.get(i).copied();
    let mut at = start;
    if at >= limit {
        return Err(malformed());
    }
    let tag = bytes[at];
    at += 1;
    if tag & 0x1f == 0x1f {
        // A tag number above 30 continues while the high bit is set.
        while at < limit && bytes[at] & 0x80 != 0 {
            at += 1;
        }
        at += 1;
    }
    let length_start = at;
    if at >= limit {
        return Err(malformed());
    }
    let first = bytes[at];
    at += 1;
    if first == 0x80 {
        let content_start = at;
        while !(at_byte(at) == Some(0) && at_byte(at + 1) == Some(0)) {
            at = read_element(bytes, at, limit)?.end;
            if at + 1 >= limit {
                return Err(malformed());
            }
        }
        return Ok(BerElement {
            start,
            length_start,
            content_start,
            content_end: at,
            end: at + 2,
            tag,
            indefinite: true,
        });
    }
    let mut length = usize::from(first);
    if first > 0x80 {
        let count = usize::from(first & 0x7f);
        if count > 6 || at + count > limit {
            return Err(malformed());
        }
        length = 0;
        for _ in 0..count {
            length = length * 256 + usize::from(bytes[at]);
            at += 1;
        }
    }
    if at + length > limit {
        return Err(malformed());
    }
    Ok(BerElement {
        start,
        length_start,
        content_start: at,
        content_end: at + length,
        end: at + length,
        tag,
        indefinite: false,
    })
}

/// `childrenOf`: the elements a constructed element holds, in order.
pub fn children_of(bytes: &[u8], parent: &BerElement) -> Result<Vec<BerElement>, ParseError> {
    let mut children = Vec::new();
    let mut at = parent.content_start;
    while at < parent.content_end {
        let child = read_element(bytes, at, parent.content_end)?;
        at = child.end;
        children.push(child);
    }
    Ok(children)
}

/// A definite length, in the fewest octets (DER).
fn encode_length(length: usize) -> Vec<u8> {
    if length < 0x80 {
        return vec![length as u8];
    }
    let mut octets = Vec::new();
    let mut rest = length;
    while rest > 0 {
        octets.insert(0, (rest % 256) as u8);
        rest /= 256;
    }
    let mut out = vec![0x80 | octets.len() as u8];
    out.extend(octets);
    out
}

/// `encodeElement`: a one-octet tag, a definite length and `content`.
pub fn encode_element(tag: u8, content: &[u8]) -> Vec<u8> {
    let mut out = vec![tag];
    out.extend(encode_length(content.len()));
    out.extend_from_slice(content);
    out
}

/// `withContent`: `element` with other content.
fn with_content(bytes: &[u8], element: &BerElement, content: &[u8]) -> Vec<u8> {
    let mut out = bytes[element.start..element.length_start].to_vec();
    if element.indefinite {
        out.push(0x80);
        out.extend_from_slice(content);
        out.extend([0, 0]);
    } else {
        out.extend(encode_length(content.len()));
        out.extend_from_slice(content);
    }
    out
}

/// `withChild`: `parent` with the bytes of one of its children replaced.
fn with_child(
    bytes: &[u8],
    parent: &BerElement,
    child: &BerElement,
    replacement: &[u8],
) -> Vec<u8> {
    let mut content = bytes[parent.content_start..child.start].to_vec();
    content.extend_from_slice(replacement);
    content.extend_from_slice(&bytes[child.end..parent.content_end]);
    with_content(bytes, parent, &content)
}

/// `addUnsignedAttribute`: one attribute (its DER) added to the unsigned
/// attributes of a CMS's one signer, where DER orders it.
pub fn add_unsigned_attribute(cms: &[u8], attribute: &[u8]) -> Result<Vec<u8>, ParseError> {
    let unexpected =
        |what: &str| ParseError::Cms { reason: "malformed", message: format!("CMS has no {what}") };
    let content_info = read_element(cms, 0, cms.len())?;
    let content = children_of(cms, &content_info)?
        .into_iter()
        .find(|c| c.tag == 0xa0)
        .ok_or_else(|| unexpected("content"))?;
    let signed_data = children_of(cms, &content)?
        .into_iter()
        .next()
        .filter(|s| s.tag == 0x30)
        .ok_or_else(|| unexpected("SignedData"))?;
    let signer_infos = children_of(cms, &signed_data)?
        .into_iter()
        .next_back()
        .filter(|s| s.tag == 0x31)
        .ok_or_else(|| unexpected("signers"))?;
    let signers = children_of(cms, &signer_infos)?;
    if signers.len() != 1 || signers[0].tag != 0x30 {
        return Err(ParseError::Cms {
            reason: "unsupported",
            message: format!("CMS has {} signers; exactly one is expected", signers.len()),
        });
    }
    let signer_info = signers[0];
    let unsigned = children_of(cms, &signer_info)?.into_iter().find(|c| c.tag == 0xa1);
    let grown_signer = match unsigned {
        Some(unsigned) => {
            let present = children_of(cms, &unsigned)?;
            let next = present.iter().find(|child| {
                crate::cms::der_order(&cms[child.start..child.end], attribute)
                    != std::cmp::Ordering::Less
            });
            let at = next.map_or(unsigned.content_end, |n| n.start);
            let mut content = cms[unsigned.content_start..at].to_vec();
            content.extend_from_slice(attribute);
            content.extend_from_slice(&cms[at..unsigned.content_end]);
            let grown_set = with_content(cms, &unsigned, &content);
            with_child(cms, &signer_info, &unsigned, &grown_set)
        }
        None => {
            let mut content = cms[signer_info.content_start..signer_info.content_end].to_vec();
            content.extend(encode_element(0xa1, attribute));
            with_content(cms, &signer_info, &content)
        }
    };
    let grown_signers = with_child(cms, &signer_infos, &signer_info, &grown_signer);
    let grown_signed_data = with_child(cms, &signed_data, &signer_infos, &grown_signers);
    let grown_content = with_child(cms, &content, &signed_data, &grown_signed_data);
    let mut grown = with_child(cms, &content_info, &content, &grown_content);
    // What follows the CMS is kept, unless it is only the zeros a service padded it with.
    let trailing = &cms[content_info.end..];
    if trailing.iter().any(|&octet| octet != 0) {
        grown.extend_from_slice(trailing);
    }
    Ok(grown)
}
