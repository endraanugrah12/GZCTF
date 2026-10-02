# Freeze, evidence and scoring behavior

- Blood notifications created during the freeze window show `Anonymous team`
  to players, through both live notifications and notice polling. Original notices
  remain stored for audit. Monitors can see original notices through the privileged
  polling endpoint; historical pre-freeze bloods are unchanged.
- The challenges page keeps the public scores frozen, but reads the signed-in
  team's own solves live. It marks post-freeze solves as normal solves without
  exposing live blood tiers. The private result is not cached as a public response.
- Submission evidence fields follow the server's current policy. The form polls
  while open and rechecks on submission. When disabled, it neither requires nor
  uploads LLM links/solver files. When enabled, the backend still enforces evidence.
- New challenges use **CTFd (parabolic)** scoring: 500 initial, 100 minimum,
  decay 50. Set `n = max(0, solves - 1)` and calculate
  `ceil(max(minimum, initial + (minimum-initial)*(n/decay)^2))`.
  The first solve always keeps the initial value. At 5/10/25/50/51 solves,
  decay 50 gives 498/488/408/116/100. With decay 10 the first three solve counts
  give 500/496/484, reaching 100 at 11 total solves. This matches
  [CTFd's implementation, including its first-solve offset](https://github.com/CTFd/CTFd/blob/master/CTFd/plugins/dynamic_challenges/decay.py).
  All previous solvers share the updated challenge value; blood bonuses are separate.

Existing challenge curves and scores are not silently migrated. To change an
existing challenge, open its admin scoring form, select **CTFd (parabolic)**,
set **Decay (solves to minimum)** to 50 (or your preferred number), and save.
Changing scoring during a game recalculates standings; coordinate this with
participants first. Standard, linear and logarithmic remain available.

Challenge exports now preserve the curve explicitly for reimports. Legacy
structured exports without a curve retain Standard scoring for compatibility.
No database schema migration is required. Rebuild the application to deploy.

The solve-count correction affects existing challenges already using CTFd scoring
when their scoreboard is regenerated. Other curves and saved decay values are unchanged.
