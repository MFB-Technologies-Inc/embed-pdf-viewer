// An annotation box as both API generations report it in page space,
// `[x, y, width, height]` (y down, in pt) rounded to 0.1 pt, so the gate
// compares one form.
export type Box = [number, number, number, number];

export function boxOf(rect: unknown): Box {
  const r = rect as { x: number; y: number; width: number; height: number };
  if (![r?.x, r?.y, r?.width, r?.height].every(Number.isFinite)) {
    throw new Error(`not an x/y/width/height rect: ${JSON.stringify(rect)}`);
  }
  const round = (n: number) => Math.round(n * 10) / 10;
  return [round(r.x), round(r.y), round(r.width), round(r.height)];
}
