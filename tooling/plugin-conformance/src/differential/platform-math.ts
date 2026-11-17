/**
 * The differential rule for the ledger's platform math entries
 * (`PLATFORM-MATH-COS-SIN`, `PLATFORM-MATH-ATAN2`): Rust calls the platform
 * math library where the TypeScript calls the JavaScript runtime's `Math`,
 * and the two can round the last bit differently.
 *
 * The rule does not compare with a tolerance. It runs the TypeScript again
 * with the named `Math` functions answering the runtime's value or a double
 * adjacent to it, chosen once per distinct argument list, and accepts Rust's
 * outcome only when it is exactly one of those runs. Each library returns
 * one of the two doubles around the exact result (error below one ulp), so
 * the two answers are equal or adjacent; every other operation is the
 * TypeScript's own, so a pivot of 4.9e15 that turns one ulp of `cos` into
 * a whole unit of the translation is accepted exactly, and a translation
 * off by anything else is not.
 */
import { diff } from '../canonical';

/** The `Math` functions the rule can move. */
export type MathName = 'cos' | 'sin' | 'atan2';

/** More distinct calls than this and the rule refuses: 3^6 reruns is the
 *  most it spends on one input. */
const MAX_CALLS = 6;

const bits = new DataView(new ArrayBuffer(8));

/** The double one step from `x` toward `+Infinity` (`1`) or `-Infinity`
 *  (`-1`); NaN and the infinities stay. */
export function adjacent(x: number, step: -1 | 0 | 1): number {
  if (step === 0 || !Number.isFinite(x)) return x;
  if (x === 0) return step * Number.MIN_VALUE;
  bits.setFloat64(0, x);
  const away = x > 0 === step > 0;
  bits.setBigUint64(0, bits.getBigUint64(0) + (away ? 1n : -1n));
  return bits.getFloat64(0);
}

/** An argument list as a key; `-0` and `0` are different arguments. */
function keyOf(name: MathName, args: readonly number[]): string {
  return `${name}(${args.map((a) => (Object.is(a, -0) ? '-0' : String(a))).join(',')})`;
}

const math = Math as unknown as Record<MathName, (...args: number[]) => number>;

/** `run` with each named `Math` function answering `answer(key, value)`. */
function withMath<T>(
  names: readonly MathName[],
  answer: (key: string, value: number) => number,
  run: () => T,
): T {
  const saved = names.map((name) => math[name]);
  try {
    names.forEach((name, i) => {
      const original = saved[i];
      math[name] = (...args: number[]) => answer(keyOf(name, args), original(...args));
    });
    return run();
  } finally {
    names.forEach((name, i) => {
      math[name] = saved[i];
    });
  }
}

/**
 * Whether `rust` is exactly what `rerun` returns when every call of the
 * named `Math` functions answers the runtime's value or an adjacent double.
 * `rerun` calls the TypeScript on the input that differed and returns its
 * outcome as a vector records it.
 */
export function withinOneUlp(
  names: readonly MathName[],
  rerun: () => unknown,
  rust: unknown,
): boolean {
  const keys: string[] = [];
  withMath(
    names,
    (key, value) => {
      if (!keys.includes(key)) keys.push(key);
      return value;
    },
    rerun,
  );
  if (keys.length === 0 || keys.length > MAX_CALLS) return false;
  const steps = keys.map(() => -1 as -1 | 0 | 1);
  for (;;) {
    if (steps.some((step) => step !== 0)) {
      // A call the moved answers lead to that the first run did not make
      // keeps the runtime's value.
      const outcome = withMath(
        names,
        (key, value) => adjacent(value, steps[keys.indexOf(key)] ?? 0),
        rerun,
      );
      if (diff(outcome, rust) === null) return true;
    }
    let i = 0;
    while (i < steps.length && steps[i] === 1) steps[i++] = -1;
    if (i === steps.length) return false;
    steps[i]++;
  }
}
