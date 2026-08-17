# Verification strategy

## Test layers

### Unit tests

Test tokenizer, breakpoint detection, prefix sums, range normalization, ratio, feasibility, badness, fitness class, penalties, demerits, comparer/tie-breaks, and path reconstruction independently.

### Algorithm tests

Run both breakers over constructed item sequences. Avoid relying only on prose paragraphs when exact widths matter; construct boxes/glue/penalties directly for mathematical cases.

### Exhaustive oracle

For small breakpoint counts, enumerate every legal start-to-end break path, independently measure it, calculate transition costs, and choose the minimum using the same documented tie-break policy but separately implemented control flow. Compare DP status, total demerits, fitness sequence, and path against the oracle.

### Integration tests

Invoke the CLI process in a temporary directory. Check exit code, stdout/stderr contract, artifact set, JSON schema fields, XML well-formedness, HTML escaping, and repeat-run determinism.

### Golden tests

Snapshot stable structural output for one tiny paragraph. Avoid snapshotting huge breakpoint graphs. Normalize line endings and never update goldens automatically during an ordinary test run.

## Required catalogue

| Area | Cases |
|---|---|
| Tokenizer | one word; repeated whitespace; tabs/newlines; punctuation remains in box; empty input |
| Measurement | exact width; positive stretch; negative shrink; zero stretch/shrink; trailing glue excluded; penalty width included only when selected |
| Feasibility | `r=-1`; just below `-1`; `r=max`; just above max; epsilon boundaries |
| Badness | `r=0`, `0.5`, `1`, `2`; cubic behavior; cap |
| Penalties | positive; negative; forced sentinel not squared; forbidden excluded |
| Fitness | all four boundaries; jump of one; jump of two |
| Flags | none; one flagged; consecutive flagged |
| Greedy | farthest feasible; forced break; overfull word termination |
| DP | unique optimum; tie; unreachable final; global beats greedy; fitness-aware retained states |
| Reconstruction | increasing chain; all boxes once; final reached; corruption detected |
| Metrics | recomputation equals result; max/mean on empty failure prohibited |
| Rendering | `<>&\"'` escaped; Unicode preserved; XML parses; stable IDs; no external resource URLs |
| CLI | help; missing/dual input; invalid width; absent file; unwritable output; successful both/single algorithm |

## Property and invariant checks

- For any finite feasible ratio, badness is nonnegative and symmetric in `r` before the feasibility cutoff.
- Increasing `|r|` does not decrease badness before the cap.
- Every DP result is no more expensive than the same feasible greedy path under identical options.
- Prefix-sum measurements equal a slow direct summation oracle.
- Reconstruction concatenated with one separator between boxes reproduces normalized input words.
- Re-running identical input/options produces identical paths, trace events, JSON, HTML, and SVG.

## Flagship regression

`examples/global-vs-greedy.txt` must be tuned only through text/width selection, not by algorithm-specific options, so that greedy and DP select different feasible paths and DP has strictly lower total demerits. Once discovered, freeze its target width and expected paths in a test; numeric totals should use tolerances in unit tests and canonical serialization in golden tests.

## Commands

```powershell
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
dotnet run --project src/KnuthPlass.Cli --configuration Release --no-build -- `
  --file examples/global-vs-greedy.txt --width 32 --output artifacts --trace
```

Then parse both SVGs as XML, parse `summary.json`, and assert the HTML contains both algorithm headings and no remote resource references.

## Performance guardrail

Performance is educational, not a benchmark claim. Add one non-timing test that a paragraph with 500 words terminates and candidate count is bounded by the theoretical breakpoint-pair count times four fitness states. Optional benchmarks belong outside CI and must record hardware/runtime before claims are made.

