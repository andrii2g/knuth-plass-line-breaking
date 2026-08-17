# CLI and artifact contract

## Command

```text
knuth-plass [--text <paragraph> | --file <path>] --width <number>
            [--algorithm both|greedy|knuth-plass]
            [--output <directory>] [--trace] [--verbose]
            [--space-width <number>] [--stretch <number>] [--shrink <number>]
            [--line-penalty <number>] [--fitness-demerit <number>]
            [--flagged-demerit <number>] [--max-ratio <number>]
            [--last-line ragged|justified]
            [--max-input-length <characters>]
```

Defaults: `both`, `artifacts`, space width `1`, stretch `0.5`, shrink `0.3333333333333333`, line penalty `10`, fitness/flagged demerit `100`, maximum ratio `3`, ragged last line, and maximum input length `100000` UTF-16 code units. The CLI accepts a configured maximum from `1` through `1000000`.

Input rules:

- Exactly one of `--text` and `--file` is required.
- `--width` must be finite and greater than zero.
- Input must contain at least one non-whitespace character.
- A file is read as strict UTF-8 with an optional UTF-8 BOM; invalid bytes and UTF-16/UTF-32 BOM input produce an input error.
- `--max-input-length` is enforced for both direct text and bounded file reads before tokenization.
- Multiple paragraphs are normalized into one paragraph for MVP, with a diagnostic when line breaks were present.

## Console output

Print input summary, a comparison table, chosen paths, improvement percentage when meaningful, and artifact paths. With `--trace`, also print the trace-file path; do not flood stdout with the entire trace by default.

Never claim DP improved the baseline when totals are equal. If greedy succeeds and strict DP fails, report results separately rather than calculating a percentage.

## Exit behavior

- `0`: every requested algorithm completed successfully;
- `2`: command usage, option, text, or UTF-8 input error;
- `3`: at least one requested algorithm did not reach a feasible final layout;
- `4`: input or output I/O failure;
- `5`: unexpected internal failure.

Expected errors use a one-line stderr diagnostic. A stack trace is included only with `--verbose`. A layout failure is still rendered so the successful/failing statuses remain inspectable before exit code 3 is returned.

## Trace format

`trace.txt` begins with normalized options and breakpoint inventory. Each candidate block includes start/end, line number candidate, natural width, target, stretch, shrink, ratio or rejection reason, badness, penalty, fitness, line demerits, accumulated candidate demerits, and state action. Stable sequence numbers make decisions referenceable. For `--algorithm both --trace`, `trace.txt` contains the Knuth-Plass trace that also supplies the breakpoint graph; stdout names the trace source. A greedy-only traced run contains the greedy trace.

Example shape (illustrative, not golden numeric output):

```text
[000017] CandidateEvaluated  start=B03 end=B07
  natural=29 target=32 stretch=3.5 shrink=2.333333333333333
  ratio=0.8571428571428571 badness=62.9737609329446 fitness=Loose
[000018] StateUpdated key=(B07,Loose) predecessor=(B03,Tight)
```

## `summary.json`

Top level:

```json
{
  "schemaVersion": 1,
  "input": { "targetWidth": 32, "wordCount": 0, "breakpointCount": 0 },
  "options": {},
  "algorithms": [],
  "comparison": {},
  "artifacts": {}
}
```

Each algorithm entry includes status, break path, lines, metrics, counters, and failure when applicable. JSON uses camelCase, invariant numeric JSON tokens, stable property order, and no timestamp.

## `comparison.html`

Standalone semantic HTML with embedded CSS. Required sections:

1. title and normalized options;
2. summary comparison;
3. side-by-side layouts, collapsing to one column on narrow screens;
4. per-line width/ratio/badness/fitness annotations;
5. chosen break paths;
6. explanation of badness versus demerits;
7. links to the two SVG files and JSON/trace when present.

Use `lang="en"`, visible focus styles, sufficient contrast, and textual labels in addition to color.

## `layout-comparison.svg`

Show target-width rulers and one group per line. Boxes are rectangles sized by their synthetic widths; glue gaps use adjusted width:

\[
g'=\begin{cases}g+r\,s&r\ge0\\g+r\,h&r<0\end{cases}
\]

A selected positive-width penalty is rendered after the normalized line items. If the formula yields a negative glue width, render it as a backward overlap span and advance the following item by that signed amount; never silently clamp it to zero. Annotate ratio, badness, and fitness. Include title/description elements for accessibility. Overfull greedy lines use a visible hatch or outline plus text label.

## `breakpoint-graph.svg`

Nodes are breakpoint/fitness states, edges are feasible candidate lines, and the selected DP path is emphasized. The DP result carries a compact immutable feasible-edge projection for this artifact; full typed trace events are captured only when `--trace` is requested. Each edge label contains compact cost data. For more than the configured graph threshold, render only states/edges within the chosen path plus a bounded neighborhood and state clearly that the view is pruned; the solver and JSON counters remain complete. Greedy-only runs omit this artifact and its HTML link.

## Atomicity and overwrite policy

Render in memory, write each artifact to a temporary file inside the output directory, then replace the fixed target. Before promotion, move existing managed artifacts to temporary backups; if promotion fails, remove newly promoted files and restore those backups. Managed artifacts not produced by the current selection are removed, so a greedy-only rerun cannot leave a stale breakpoint graph or link. The CLI may overwrite or remove its own five fixed artifact names but no arbitrary files. It rejects an input file that resolves to any managed output path.

