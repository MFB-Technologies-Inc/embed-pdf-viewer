// How two builds' answers compare, from each row's digests over every
// process that ran it (`engine-timer round` prints both per row).
//
// `digest` reads numbers at the corpus quantum (1e-4 point), as the vectors
// do: a row with more than one is answered differently, and the run stops.
// `rawDigest` reads the answers as the host wrote them: a row with more than
// one at the same `digest` differs only below the quantum, and the run goes
// on with a warning that names it.

export function compareAnswers(digests, rawDigests) {
  const varies = (byRow) =>
    Object.entries(byRow)
      .filter(([, set]) => set.size > 1)
      .map(([row]) => row);
  const differing = varies(digests);
  const belowQuantum = varies(rawDigests).filter((row) => !differing.includes(row));
  return { differing, belowQuantum };
}
