/**
 * `differential.ts --crate geometry`: inputs for every function of the
 * geometry suites (`suites/geometry.ts`), answered on the Rust side by
 * `epdf_geometry::harness::functions()`.
 *
 * Numbers lean on the values the hand cases probe: NaN, `-0`, `±Infinity`,
 * subnormals and values near `1e308`, beside small integers and ordinary
 * doubles. A `-0` argument reaches both sides as `0`, since the generator
 * encodes arguments canonically before the call (`callFunctionCase`).
 */
import fc from 'fast-check';

import * as g from '../../../../packages/core/geometry/src/index';
import { suites } from '../suites/geometry';
import type { FunctionSuite } from '../vectors';
import type { CrateTable, SkipPredicate } from '../checks/differential';
import { withinOneUlp } from './platform-math';

const MIN_NORMAL = 2.2250738585072014e-308;

const special = fc.constantFrom(
  0,
  -0,
  NaN,
  Infinity,
  -Infinity,
  5e-324,
  -5e-324,
  MIN_NORMAL,
  1e308,
  -1e308,
  Number.MAX_VALUE,
  -Number.MAX_VALUE,
  0.5,
  -0.5,
  1,
  -1,
);

const signed = (magnitude: fc.Arbitrary<number>) =>
  fc.tuple(fc.boolean(), magnitude).map(([negative, x]) => (negative ? -x : x));

export const num: fc.Arbitrary<number> = fc.oneof(
  { weight: 4, arbitrary: fc.integer({ min: -1000, max: 1000 }) },
  { weight: 4, arbitrary: fc.double({ min: -1e4, max: 1e4, noNaN: true }) },
  { weight: 2, arbitrary: fc.double() },
  { weight: 1, arbitrary: special },
  { weight: 1, arbitrary: fc.double({ min: -MIN_NORMAL, max: MIN_NORMAL, noNaN: true }) },
  { weight: 1, arbitrary: signed(fc.double({ min: 1e306, max: Number.MAX_VALUE, noNaN: true })) },
);

/** Mostly the four page rotations, sometimes an odd one or any number. */
const rotation = fc.oneof(
  { weight: 4, arbitrary: fc.constantFrom(0, 90, 180, 270) },
  { weight: 2, arbitrary: fc.constantFrom(-90, -270, 360, 450, 720, 45, 90.5) },
  { weight: 1, arbitrary: num },
);

const point = fc.record({ x: num, y: num });
const rect = fc.record({ x: num, y: num, width: num, height: num });
const size = fc.record({ width: num, height: num });
const edges = fc.record({ left: num, bottom: num, right: num, top: num });
const mat = fc.tuple(num, num, num, num, num, num);
const randomQuad = fc.record({ upperLeft: point, upperRight: point, lowerLeft: point, lowerRight: point });

/** A rect's quad turned by an angle, its corners sometimes nudged: the
 *  shapes text quads take, where `normalizePdfQuad`'s checks decide. */
const textLikeQuad = fc
  .tuple(
    fc.record({
      x: fc.integer({ min: -500, max: 500 }),
      y: fc.integer({ min: -500, max: 500 }),
      width: fc.double({ min: -200, max: 200, noNaN: true }),
      height: fc.double({ min: -50, max: 50, noNaN: true }),
    }),
    fc.oneof(fc.constantFrom(0, Math.PI / 2, Math.PI, -Math.PI / 2), fc.double({ min: -7, max: 7, noNaN: true })),
    fc.array(fc.double({ min: -2e-6, max: 2e-6, noNaN: true }), { minLength: 8, maxLength: 8 }),
  )
  .map(([r, angle, nudge]) => {
    const q = g.applyQuad(g.rotateAbout({ x: r.x, y: r.y }, angle), g.quadFromRect(r));
    const at = (p: g.Point, i: number) => ({ x: p.x + nudge[2 * i], y: p.y + nudge[2 * i + 1] });
    return {
      upperLeft: at(q.upperLeft, 0),
      upperRight: at(q.upperRight, 1),
      lowerLeft: at(q.lowerLeft, 2),
      lowerRight: at(q.lowerRight, 3),
    };
  });
const quad = fc.oneof(randomQuad, textLikeQuad);

/** A matrix: any six numbers, or one built from a rotation and scale. */
const matrix = fc.oneof(
  { weight: 3, arbitrary: mat },
  {
    weight: 1,
    arbitrary: fc
      .tuple(num, num, num)
      .map(([angle, sx, sy]) => [...g.compose(g.rotate(angle), g.scale(sx, sy))] as number[]),
  },
);

const probe = fc.record({
  points: fc.array(point, { maxLength: 4 }),
  rects: fc.array(rect, { maxLength: 3 }),
});

const args = (...items: fc.Arbitrary<unknown>[]): fc.Arbitrary<unknown[]> => fc.tuple(...items);

/** Arguments for each function, by the name the suites call it. */
export const arbitraries: Record<string, fc.Arbitrary<unknown[]>> = {
  // geometry-basics.json
  NO_FRAME: args(),
  boundsOfRects: args(fc.array(rect, { maxLength: 5 })),
  intersectRects: args(rect, rect),
  rectFromCorners: args(point, point),
  rectContains: args(rect, point),
  rectsOverlap: args(rect, rect),
  isQuarterTurn: args(rotation),
  addRotations: args(rotation, rotation),
  snapToDevice: args(num, fc.oneof(fc.constantFrom(1, 2, 1.5, 3, 1.25), num)),
  displaySize: args(size, rotation),
  deviceHeightForWidth: args(size, num),

  // geometry-affine.json
  identity: args(),
  compose: args(matrix, matrix),
  invert: args(matrix),
  applyPoint: args(matrix, point),
  applyRect: args(matrix, rect),
  applyQuad: args(matrix, quad),
  matrixToCss: args(matrix),
  translate: args(num, num),
  scale: args(num, num),
  rotate: args(num),
  rotateAbout: args(point, num),
  scaleAbout: args(point, num, num),
  angleOf: args(matrix),

  // geometry-page.json
  rotateScaleMatrix: args(num, num, num, rotation),
  pageTransform: args(
    fc.record(
      { pageSize: size, rotation, scale: num, baseScale: num, dpr: num },
      { requiredKeys: ['pageSize', 'rotation', 'scale', 'dpr'] },
    ),
    probe,
  ),
  enginePageSpace: args(
    edges,
    fc.record({
      points: fc.array(point, { maxLength: 4 }),
      rects: fc.array(rect, { maxLength: 3 }),
      edges: fc.array(edges, { maxLength: 3 }),
      quads: fc.array(quad, { maxLength: 2 }),
    }),
  ),
  pdfQuadBounds: args(quad),

  // geometry-text-quad.json
  textQuadFromRect: args(rect),
  textQuadPoints: args(quad),
  textQuadRing: args(quad),
  textQuadEdge: args(quad, fc.constantFrom('left', 'right')),
  textQuadEquals: fc.oneof(
    args(quad, quad),
    quad.map((q) => [q, structuredClone(q)]),
  ),
  textQuadBounds: args(quad),
  applyTextQuad: args(matrix, quad),
  quadCorners: args(quad),
  normalizePdfQuad: fc.oneof(
    args(fc.tuple(point, point, point, point)),
    quad.map((q) => [g.quadCorners(q)]),
  ),
};

/** Ledger skips by `divergences.json` id: each accepts only the outcome the
 *  TypeScript gives when the named `Math` functions answer one ulp off
 *  (`platform-math.ts`). */
export const skips: Record<string, SkipPredicate> = {
  'PLATFORM-MATH-COS-SIN': (d) => withinOneUlp(['cos', 'sin'], d.rerun, d.rust),
  'PLATFORM-MATH-ATAN2': (d) => withinOneUlp(['atan2'], d.rerun, d.rust),
};

export const table: CrateTable = {
  table: 'geometry',
  suites: suites as FunctionSuite[],
  arbitraries,
  skips,
};
