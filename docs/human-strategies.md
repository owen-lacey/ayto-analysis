# Can a contestant play Are You The One in their head?

This tests strategies that only use what a person could track on paper: a 10×10 grid of integer scores, which
pairs are confirmed or eliminated, and past lineups with their scores. The exact set of viable matchings is used
only to **measure** bits per event and success, never to choose an event.

Full numbers: [`results/human_strategies.md`](../results/human_strategies.md). Per-season data:
[`results/log2_remaining.csv`](../results/log2_remaining.csv). Chart:

![Mean log2 remaining viable matchings per event](../results/log2_remaining.svg)

## Results

10 couples, 10 weeks of (truth booth, then match-up), 200 seasons. Every simulated strategy faces the same
200 hidden matchings and tie-break seeds, so the comparison is paired.

| Method             | Per Event | Success Rate |
|--------------------|-----------|--------------|
| Random *(quoted)*  | 1.23 bits | 74%          |
| Actual *(quoted)*  | 1.39 bits | 71%          |
| Information Theory *(quoted)* | 1.59 bits | 98% |
| Random (harness control) | 1.18 ± 0.19 bits | 55% |
| A | 0.91 ± 0.18 bits | 10% |
| A without endgame | 0.91 ± 0.18 bits | 11% |
| A, highest score | 0.83 ± 0.18 bits | 7% |
| A + B | 0.89 ± 0.21 bits | 12% |
| A + C (T=3) | 0.83 ± 0.18 bits | 6% |
| A + C (T=5) | 0.93 ± 0.19 bits | 11% |
| A + C (T=7) | 0.93 ± 0.17 bits | 9% |
| A + C (T=10) | 0.91 ± 0.18 bits | 10% |
| A + B + C (T=3) | 0.87 ± 0.20 bits | 9% |
| A + B + C (T=5) | 0.86 ± 0.19 bits | 9% |
| A + B + C (T=7) | 0.88 ± 0.18 bits | 10% |

± is the standard deviation across seasons of each season's mean bits per event. The quoted rows were supplied
with the brief and aren't reproducible from this repo (see [Caveats](#caveats)). The **harness control** plays random
lineups that avoid known-wrong pairs, like `RandomlyGenerated`. It's there to show that the harness itself isn't
penalising the strategies.

## Findings

### Which human strategy gets closest to Information Theory?
**A + C with T=5 or T=7, at 0.93 bits per event. That's not close.** It reaches about 59% of IT's 1.59 bits per event,
and the matching is determined in only 9–11% of seasons, against 98% for IT. In the paired comparison, none of the human
variants is clearly better than plain A: the T=5/T=7 gains are +0.02 ± 0.03 bits per event (95% interval).

### Does any of them beat Actual?
**No.** None beats Actual (1.39), and none even beats random lineups in the same harness (1.18 bits, 55% success).
The random control beats A in 178 of 200 paired seasons.

### Why the grid loses to random: it can't see that a lineup's pairs are correlated
Naive Bayes gives every unknown pair in a lineup the same credit. After a 3-beam match-up, all ten pairs go from
−10 to −4 log-odds and everything else stays at −10. "Closest to evens" then picks those ten pairs again, and
rebuilds **the identical lineup**, which is guaranteed to score 3 again and gives 0 bits. The grid can't
represent "these ten pairs **together** contain exactly 3 matches". It only sees ten independent 28% pairs.

| Method | Identical lineups repeated per season | Pairs changed between consecutive match-ups |
|---|---|---|
| Random (harness control) | 0.00 | 8.7 |
| A | 2.54 | 4.5 |
| A, highest score | 3.94 | 3.0 |
| A + C (T=5) | 1.61 | 4.5 |
| A + B | 0 | 2.0 (by construction) |

A random lineup changes about 9 of 10 pairs each week, so each match-up is a near-independent test with an
unpredictable score. The grid strategies keep circling the same pairs, so their scores are largely predictable
and carry little information.

### Double counting, measured
After every match-up we compared the grid's implied probability for each unknown pair, 1/(1+10^(−log-odds/10)),
with the pair's true share of the remaining viable matchings. We grouped pairs by how many match-ups they've appeared in
(strategy A, all 200 seasons):

| Grid says | Appearances | Pairs | Mean implied | Mean actual |
|---|---|---|---|---|
| 25%–50% | 1 | 1,953 | 33% | 26% |
| 25%–50% | 3+ | 827 | 36% | 15% |
| 50%–75% | 1 | 322 | 57% | 46% |
| 50%–75% | 3+ | 1,434 | 62% | 20% |
| 75%–100% | 1 | 55 | 82% | 74% |
| 75%–100% | 3+ | 5,717 | 91% | **32%** |

After one appearance the grid is roughly calibrated. Each repeat of the same lineup re-adds the same evidence, so
a pair that has sat in three similar lineups looks like a 91% favourite but is a match only 32% of the time.
Other symptoms:
- **8%** of A's row favourites (a unique top score at or above evens) were already **impossible**. With
  highest-score selection or swaps, that rises to 17–22%.
- **7%** of the pairs the grid still marks unknown had already been ruled out by the exact model. The grid only
  drops a pair after a truth booth, a blackout, a swap deduction, or a single remaining candidate in a row or column. It
  never revisits old lineups once later confirmations change what they imply.

### How sensitive are the results to T?
Hardly at all, between 5 and 10. T=3 is the only clear outlier, and it's worse.

| T | Bits/event | Δ vs A (T=10), paired, 95% | Success |
|---|---|---|---|
| 3 | 0.83 | −0.078 ± 0.034 | 6% |
| 5 | 0.93 | +0.021 ± 0.035 | 11% |
| 7 | 0.93 | +0.024 ± 0.031 | 9% |
| 10 (= A) | 0.91 | 0 | 10% |

T=10 reproduces A exactly, which is a built-in sanity check. Padding needs known zeros in the right rows and columns,
and there are few of them early on, so the first weeks mostly fall back to A. With T=3, a match-up's unknown pairs can score
only 0–3, so it carries at most log2(4) = 2 bits. Spending the whole ceremony on three pairs loses more than the
cleaner reading gains.

### The other ablations
- **Endgame rule: no effect** (+0.005 ± 0.006, and only 14 of 200 seasons differ). The trigger, every open row
  with a favourite 10 points clear, almost never fires before the season ends.
- **Highest score instead of closest to evens: worse** (−0.074 ± 0.021). It doubles down on the inflated favourites
  and repeats lineups most often (3.9 per season).
- **Swaps (B): no measurable change** (−0.02 ± 0.035). The deductions are exact and B never repeats a lineup. But
  changing only two pairs means one match-up can rule out at most a small slice of the space: the score change takes
  one of five values, so it's worth at most log2(5) ≈ 2.3 bits. Combining with padding (A + B + C) is slightly worse again
  (−0.03 to −0.05).

#### Why a swap's score change is always decisive
Only the two swapped slots differ, so Δ = ([A-b] + [B-a]) − ([A-a] + [B-b]), where each bracket is 0 or 1. If A-a were a match,
A can't also match b and a can't also match B, so both new pairs must be 0 and Δ ≤ −1. The same goes for B-b. So:

| Δ | Deduction |
|---|---|
| −2 | A-a and B-b confirmed |
| −1 | exactly one of A-a, B-b matches; A-b and B-a eliminated; truth booth A-a next |
| 0 | all four pairs eliminated |
| +1 | exactly one of A-b, B-a matches; A-a and B-b eliminated; truth booth A-b next |
| +2 | A-b and B-a confirmed |

`SwapDeductionTests` checks these against every hidden matching of 5 couples.

### Takeaway for a contestant
The grid's arithmetic is fine. Its **lineup choice** is the problem. The cheapest fix the data points to is a
rule a human can easily follow: change most of the lineup every week, and never re-run a lineup (or most of one)
whose score you already know. Random lineups following only "avoid known-wrong pairs" beat every grid rule tested here.
That rule is a hypothesis from these results and wasn't tested as a strategy.

## Method notes
- **Measurement:** `ViableSet` holds all 10! matchings and filters them after each event. A test checks that
  it matches the CP-SAT `Solver`'s solution counts. Bits per event uses the same definition as `Program.CalculateInformationGained`, now shared as
  `InformationMetrics.MeanBitsPerEvent`: the mean of −log2(count_t / count_{t−1}) over events while more than one
  matching remained. Success means one matching remains after the week-10 truth booth.
- **Grid rules** follow the brief, with these interpretations:
  - When every unknown pair in a lineup must score (k′ = n′), all of them are confirmed.
  - A row or column with a single candidate left is confirmed.
  - The row prior round(10·log10(1/(r−1))) is rounded to an integer.
- **Increment table:** with n′ = 10 the formula gives 0 / **+4** / +6 / +8 for k′ = 1–4. The brief's rule of thumb said +3
  for k′ = 2 (10·log10(2.25) = 3.52). The formula was used.
- **Strategy C** picks its T test pairs closest to evens, but skips any pair that would make padding impossible.
  It pads with an augmenting-path matching on eliminated pairs, and anything still unfilled falls back to A.
- **Strategy B** picks the swap minimising the summed |log-odds| of the four pairs. Pairs already known cost a flat
  30 each, and swaps touching confirmed pairs are skipped. If no useful swap exists, it builds a fresh lineup.
- **A + B + C** is B with C's padded lineup as the starting point and fallback. Ties throughout are broken by a
  per-season seeded RNG that is shared across strategies.

## Caveats
- **The quoted Random, Actual and IT rows can't be reproduced from this repo.** The existing generators aren't seeded,
  run until solved rather than for a fixed 10 weeks, and have no success metric. `InformationTheory.cs` is set to 6 couples,
  and its "failure" branch evaluates `Result = true` twice. The harness control (55%) is lower than the quoted Random
  (74%), probably because of the fixed schedule and the success definition, so compare the quoted rows only loosely.
- 200 seasons give success rates a standard error of about ±2 percentage points at 10%.

## Reproduce
```bash
dotnet test
dotnet run --project AreYouTheOne.Console -c Release -- human --runs 200 --seed 1000 --out results
```
Takes about 2 minutes on 4 cores. Runs are deterministic for a given seed.
