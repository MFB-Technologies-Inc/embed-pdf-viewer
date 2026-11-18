// Writes large.pdf: 400 US Letter pages, each with one filled rectangle and 40
// lines of Courier text carrying the page number and the word `the` a fixed
// number of times. Then annots.pdf, the same pages with 1,200 annotations on
// them, and forms.pdf, 100 pages with 1,000 AcroForm fields. Hand-written
// objects, so no PDF library computes the files. The output is committed: a
// generator change shows up as a changed hash in fixtures.json rather than as
// a silently different fixture.
import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';

// Copied from tooling/viewer-e2e/fixtures/generate.mjs on
// research/multi-platform-rust-core-impl: a tiny PDF writer, objects in, a
// valid file out.
function build(objects, rootIndex = 1) {
  let pdf = '%PDF-1.4\n';
  const offsets = [0];
  objects.forEach((obj, i) => {
    offsets.push(Buffer.byteLength(pdf));
    pdf += `${i + 1} 0 obj\n${obj}\nendobj\n`;
  });
  const xref = Buffer.byteLength(pdf);
  pdf += `xref\n0 ${objects.length + 1}\n0000000000 65535 f \n`;
  pdf += offsets
    .slice(1)
    .map((n) => `${String(n).padStart(10, '0')} 00000 n \n`)
    .join('');
  pdf += `trailer\n<< /Size ${objects.length + 1} /Root ${rootIndex} 0 R >>\nstartxref\n${xref}\n%%EOF\n`;
  return pdf;
}

export const PAGES = 400;
export const LINES = 40;
// Each line has `the` twice, so a search for `the` finds 80 per page.
const line = (page, n) => `Page ${page} line ${n}: the quick fox and the lazy dog`;

// large.pdf's pages. `annots(p, add)`, when given, adds page p's (1-based)
// annotation objects and returns their object numbers.
function textPages(annots) {
  const objects = [];
  const add = (s) => (objects.push(s), objects.length);
  add('<< /Type /Catalog /Pages 2 0 R >>');
  add('');
  add('<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>');
  const pages = [];
  for (let p = 1; p <= PAGES; p++) {
    let text = 'BT /F1 11 Tf 13 TL 54 730 Td\n';
    for (let n = 1; n <= LINES; n++) text += `(${line(p, n)}) Tj T*\n`;
    text += 'ET\n';
    // Vector content that differs per page, so no two pages rasterize alike.
    const shade = ((p * 37) % 100) / 100;
    const content = `q ${shade} 0.4 ${1 - shade} rg 430 40 128 128 re f Q\n${text}`;
    const stream = add(`<< /Length ${Buffer.byteLength(content)} >>\nstream\n${content}endstream`);
    const list = annots ? annots(p, add) : [];
    const annotsEntry = list.length ? ` /Annots [${list.map((a) => `${a} 0 R`).join(' ')}]` : '';
    pages.push(
      add(
        `<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R >> >> /Contents ${stream} 0 R${annotsEntry} >>`,
      ),
    );
  }
  objects[1] = `<< /Type /Pages /Count ${pages.length} /Kids [${pages.map((p) => `${p} 0 R`).join(' ')}] >>`;
  return objects;
}

writeFileSync(new URL('large.pdf', import.meta.url), build(textPages()));

// annots.pdf: large.pdf's pages with 1,000 highlights, 150 ink drawings and 50
// stamps. Page index 200 has 20 highlights, on lines 1 to 20 (`hover`); the
// other 399 pages share the other 980, two or three each, on lines 1, 5 and 9.
// Ink drawings go on every page index 8k + 1, three each, right of the text;
// stamps on every page index 8k + 5, one each, right of the text in the top
// half of the page, which a 1280 x 800 window shows at zoom 1 (`dragAnnot`).
// Page indexes 202 and 208 have neither (`drawInk`, `undo`).
const ANNOTS = { highlights: 1000, ink: 150, stamps: 50, hoverPage: 200 };
const num = (n) => Number(n.toFixed(2));
// Line n's baseline is at 730 - 13 (n - 1); a Courier 11 glyph is 6.6 pt wide.
function highlight(p, n) {
  const base = 730 - 13 * (n - 1);
  const [x1, x2, y1, y2] = [54, num(54 + 6.6 * line(p, n).length), base - 3, base + 9];
  return `<< /Type /Annot /Subtype /Highlight /Rect [${x1} ${y1} ${x2} ${y2}] /QuadPoints [${x1} ${y2} ${x2} ${y2} ${x1} ${y1} ${x2} ${y1}] /C [1 0.9 0] /F 4 /NM (hl-${p}-${n}) /T (perf) >>`;
}
function ink(p, k) {
  // A 20-point zig-zag in a 142 x 100 pt box, the three boxes one above the other.
  const [x0, y0] = [420, 560 - 140 * k];
  const pts = Array.from({ length: 20 }, (_, i) => [num(x0 + i * 7.5), y0 + (i % 2 ? 100 : 0) - ((p + i) % 5) * 4]);
  const xs = pts.map(([x]) => x);
  const ys = pts.map(([, y]) => y);
  const rect = [Math.min(...xs) - 2, Math.min(...ys) - 2, num(Math.max(...xs) + 2), Math.max(...ys) + 2];
  return `<< /Type /Annot /Subtype /Ink /Rect [${rect.join(' ')}] /InkList [[${pts.flat().join(' ')}]] /C [0 0.3 1] /BS << /W 2 >> /F 4 /NM (ink-${p}-${k}) /T (perf) >>`;
}
{
  let spare = ANNOTS.highlights - 20 - 2 * (PAGES - 1);
  const counts = { highlights: 0, ink: 0, stamps: 0 };
  let stampLook = null;
  const objects = textPages((p, add) => {
    const index = p - 1;
    const list = [];
    if (index === ANNOTS.hoverPage) {
      for (let n = 1; n <= 20; n++) list.push(add(highlight(p, n)));
      counts.highlights += 20;
    } else {
      const n = spare-- > 0 ? 3 : 2;
      for (let i = 0; i < n; i++) list.push(add(highlight(p, 1 + 4 * i)));
      counts.highlights += n;
    }
    if (index % 8 === 1) {
      for (let k = 0; k < 3; k++) list.push(add(ink(p, k)));
      counts.ink += 3;
    }
    if (index % 8 === 5) {
      if (stampLook === null) {
        const look = 'q 0.8 0.1 0.1 RG 0.98 0.9 0.9 rg 3 w 2 2 136 46 re B Q\n';
        stampLook = add(
          `<< /Type /XObject /Subtype /Form /BBox [0 0 140 50] /Length ${Buffer.byteLength(look)} >>\nstream\n${look}endstream`,
        );
      }
      list.push(
        add(
          `<< /Type /Annot /Subtype /Stamp /Rect [420 520 560 570] /Name /Approved /AP << /N ${stampLook} 0 R >> /F 4 /NM (stamp-${p}) /T (perf) >>`,
        ),
      );
      counts.stamps += 1;
    }
    return list;
  });
  const want = { highlights: ANNOTS.highlights, ink: ANNOTS.ink, stamps: ANNOTS.stamps };
  if (JSON.stringify(counts) !== JSON.stringify(want)) {
    throw new Error(`annots.pdf has ${JSON.stringify(counts)}, not ${JSON.stringify(want)}`);
  }
  writeFileSync(new URL('annots.pdf', import.meta.url), build(objects));
}

// forms.pdf: 100 pages, each with 5 text fields and 5 checkboxes in two
// columns, named t<page>-<row> and c<page>-<row> (page 1-based): 1,000 fields,
// each its own widget with appearance streams of its own (PDFium rewrites a
// text widget's stream when its value changes, so a shared one would show
// the value in every field that uses it).
const FORMS = { pages: 100, rows: 5 };
{
  const objects = [];
  const add = (s) => (objects.push(s), objects.length);
  add(''); // catalog, written last
  add(''); // page tree, written last
  const helv = add('<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>');
  const stream = (dict, body) => add(`<< ${dict}/Length ${Buffer.byteLength(body)} >>\nstream\n${body}endstream`);
  const box = (w) => `q 0.5 G 1 w 0.5 0.5 ${w - 1} 19 re S Q\n`;
  const textLook = () => stream('/Type /XObject /Subtype /Form /BBox [0 0 240 20] ', box(240));
  const offLook = () => stream('/Type /XObject /Subtype /Form /BBox [0 0 20 20] ', box(20));
  const onLook = () => stream('/Type /XObject /Subtype /Form /BBox [0 0 20 20] ', `${box(20)}q 0 G 2 w 4 10 m 8 5 l 16 15 l S Q\n`);
  const fields = [];
  const pages = [];
  for (let p = 1; p <= FORMS.pages; p++) {
    // The page object comes after its widgets, their 3 appearance streams a
    // row, and its content stream.
    const page = objects.length + 5 * FORMS.rows + 2;
    let labels = 'BT /F1 11 Tf\n';
    const widgets = [];
    for (let r = 1; r <= FORMS.rows; r++) {
      const y = 760 - 60 * r;
      labels += `1 0 0 1 54 ${y + 5} Tm (Page ${p} field ${r}) Tj\n`;
      const [text, on, off] = [textLook(), onLook(), offLook()];
      widgets.push(
        add(
          `<< /Type /Annot /Subtype /Widget /FT /Tx /T (t${p}-${r}) /V () /DA (/Helv 10 Tf 0 g) /Rect [170 ${y} 410 ${y + 20}] /P ${page} 0 R /F 4 /AP << /N ${text} 0 R >> >>`,
        ),
        add(
          `<< /Type /Annot /Subtype /Widget /FT /Btn /T (c${p}-${r}) /V /Off /AS /Off /Rect [440 ${y} 460 ${y + 20}] /P ${page} 0 R /F 4 /AP << /N << /Yes ${on} 0 R /Off ${off} 0 R >> >> >>`,
        ),
      );
    }
    labels += 'ET\n';
    const content = stream('', labels);
    pages.push(
      add(
        `<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 ${helv} 0 R >> >> /Contents ${content} 0 R /Annots [${widgets.map((w) => `${w} 0 R`).join(' ')}] >>`,
      ),
    );
    if (pages.at(-1) !== page) throw new Error(`forms.pdf page ${p} is object ${pages.at(-1)}, its widgets say ${page}`);
    fields.push(...widgets);
  }
  if (fields.length !== 2 * FORMS.rows * FORMS.pages) throw new Error(`forms.pdf has ${fields.length} fields`);
  objects[0] = `<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [${fields.map((f) => `${f} 0 R`).join(' ')}] /DA (/Helv 10 Tf 0 g) /DR << /Font << /Helv ${helv} 0 R >> >> >> >>`;
  objects[1] = `<< /Type /Pages /Count ${pages.length} /Kids [${pages.map((p) => `${p} 0 R`).join(' ')}] >>`;
  writeFileSync(new URL('forms.pdf', import.meta.url), build(objects));
}

// fixtures.json: name -> sha256, recomputed and checked at every run start.
const hash = (name) => createHash('sha256').update(readFileSync(new URL(`${name}.pdf`, import.meta.url))).digest('hex');
const fixtures = { ebook: hash('ebook'), large: hash('large'), annots: hash('annots'), forms: hash('forms') };
writeFileSync(new URL('fixtures.json', import.meta.url), JSON.stringify(fixtures, null, 2) + '\n');
console.log(fixtures);
