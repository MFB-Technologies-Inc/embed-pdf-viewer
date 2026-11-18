import { createViewer, buildInfo } from '@mfb/embedpdf-rust-viewer';
const mode = new URLSearchParams(location.search).get('src') ?? 'url';
let src = '/doc.pdf';
if (mode === 'bytes') src = new Uint8Array(await (await fetch('/doc.pdf')).arrayBuffer());
const el = await createViewer({ target: '#viewer', src });
window.__buildInfo = buildInfo;
el.addEventListener('epdf:ready', () => { window.__viewer = el.viewer; });
if (el.viewer) window.__viewer = el.viewer;
