/**
 * The differential rule for the ledger's platform math entries
 * (`PLATFORM-MATH-COS-SIN`, `PLATFORM-MATH-ATAN2`): Rust calls the platform
 * math library where the TypeScript calls the JavaScript runtime's `Math`,
 * and the two can round the last bit differently.
 *
 * The rule does not compare with a tolerance, and it does not search. It
 * records the distinct calls of the named `Math` functions the TypeScript
 * makes on the input, asks `function-server` what the platform library
 * answers to each, and runs the TypeScript once more with exactly those
 * answers. Rust's outcome is accepted only when it is that run's, and only
 * when every platform answer is the runtime's or a double next to it. Every
 * other operation is the TypeScript's own, so a pivot of 4.9e15 that turns
 * one ulp of `cos` into a whole unit of the translation is accepted exactly,
 * and anything else Rust does around the call is not.
 *
 * It assumes the ported code calls the platform library as the probe does
 * (`f64::cos`, `f64::sin`, `y.atan2(x)` on the same arguments): the probe
 * reports the library, not the port's call of it.
 */
import { diff } from '../canonical';
import type { Difference, SkipVerdict } from '../checks/differential';

/** The `Math` functions the rule can move. */
export type MathName = 'cos' | 'sin' | 'atan2';

/** More distinct calls than this and the rule refuses rather than ask the
 *  server without end; the geometry functions make at most four. */
const MAX_CALLS = 64;

/** Reruns before the calls the TypeScript makes must stop changing. */
const MAX_RERUNS = 4;

const view = new DataView(new ArrayBuffer(8));

/** A double as the 16 lowercase hex digits of its bits, as `function-server`
 *  takes and gives a math probe's numbers. */
export function toBits(x: number): string {
  view.setFloat64(0, x);
  return view.getBigUint64(0).toString(16).padStart(16, '0');
}

export function fromBits(bits: string): number {
  if (!/^[0-9a-f]{16}$/.test(bits))
    throw new Error(`not the bits of a double: ${JSON.stringify(bits)}`);
  view.setBigUint64(0, BigInt(`0x${bits}`));
  return view.getFloat64(0);
}

/** The double one step from `x` toward `+Infinity` (`1`) or `-Infinity`
 *  (`-1`); NaN and the infinities stay. */
export function adjacent(x: number, step: -1 | 0 | 1): number {
  if (step === 0 || !Number.isFinite(x)) return x;
  if (x === 0) return step * Number.MIN_VALUE;
  view.setFloat64(0, x);
  const away = x > 0 === step > 0;
  view.setBigUint64(0, view.getBigUint64(0) + (away ? 1n : -1n));
  return view.getFloat64(0);
}

/** Whether `platform` is `runtime` or a double next to it; `-0` and `0`
 *  are different answers. */
function withinOneUlp(platform: number, runtime: number): boolean {
  return [runtime, adjacent(runtime, -1), adjacent(runtime, 1)].some((x) => Object.is(x, platform));
}

/** A double printed so `-0` and the last bit show. */
function show(x: number): string {
  return Object.is(x, -0) ? '-0' : String(x);
}

interface Call {
  readonly name: MathName;
  readonly args: readonly number[];
  /** What the JavaScript runtime answers. */
  readonly runtime: number;
}

/** An argument list as a key; `-0` and `0` are different arguments. */
function keyOf(name: MathName, args: readonly number[]): string {
  return `${name}(${args.map(show).join(', ')})`;
}

const math = Math as unknown as Record<MathName, (...args: number[]) => number>;

/** `run` with each named `Math` function answering `answer(call)`, given the
 *  runtime's own answer. `Math` is restored however `run` ends. */
function withMath<T>(
  names: readonly MathName[],
  answer: (key: string, call: Call) => number,
  run: () => T,
): T {
  const saved = names.map((name) => math[name]);
  try {
    names.forEach((name, i) => {
      const original = saved[i];
      math[name] = (...args: number[]) =>
        answer(keyOf(name, args), { name, args, runtime: original(...args) });
    });
    return run();
  } finally {
    names.forEach((name, i) => {
      math[name] = saved[i];
    });
  }
}

/**
 * Whether `difference.rust` is exactly what the TypeScript returns when each
 * call of the named `Math` functions answers what the platform library
 * answers, each answer within one ulp of the runtime's. A refusal says why.
 */
export async function platformMath(
  names: readonly MathName[],
  difference: Difference,
): Promise<SkipVerdict> {
  const calls = new Map<string, Call>();
  const record = (key: string, call: Call) => {
    if (!calls.has(key)) calls.set(key, call);
    return call.runtime;
  };
  withMath(names, record, difference.rerun);
  if (calls.size === 0) {
    return { refused: `the TypeScript calls no Math.${names.join(', Math.')} on this input` };
  }
  const platform = new Map<string, number>();
  for (let rerun = 0; rerun < MAX_RERUNS; rerun++) {
    if (calls.size > MAX_CALLS)
      return { refused: `${calls.size} distinct Math calls, more than ${MAX_CALLS}` };
    for (const [key, call] of calls) {
      if (platform.has(key)) continue;
      let answer: number;
      try {
        answer = await difference.math(call.name, call.args);
      } catch (error) {
        return { refused: `${key}: function-server: ${(error as Error).message}` };
      }
      if (!withinOneUlp(answer, call.runtime)) {
        return {
          refused: `${key}: the platform library answers ${show(answer)}, the runtime ${show(call.runtime)}, more than one ulp apart`,
        };
      }
      platform.set(key, answer);
    }
    // A platform answer can lead the TypeScript to a call the first run did
    // not make; it is asked for in the next round, and the outcome counts
    // only from a run that made no call left unasked.
    let unasked = false;
    const outcome = withMath(
      names,
      (key, call) => {
        if (platform.has(key)) return platform.get(key)!;
        unasked = true;
        return record(key, call);
      },
      difference.rerun,
    );
    if (unasked) continue;
    const differs = diff(outcome, difference.rust);
    return differs === null
      ? true
      : {
          refused: `Rust's outcome is not the TypeScript's with the platform library's answers: ${differs}`,
        };
  }
  return { refused: `the Math calls kept changing after ${MAX_RERUNS} reruns` };
}
