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
  decay 50. The formula is `ceil(max(minimum, initial + (minimum-initial)*(solves/decay)^2))`.
  At 5/10/25/50 solves the values are 496/484/400/100. This follows
  [CTFd's documented dynamic-value formula](https://docs.ctfd.io/docs/custom-challenges/dynamic-value/).

Existing challenge curves and scores are not silently migrated. To change an
existing challenge, open its admin scoring form, select **CTFd (parabolic)**,
set **Decay (solves to minimum)** to 50 (or your preferred number), and save.
Changing scoring during a game recalculates standings; coordinate this with
participants first. Standard, linear and logarithmic remain available.

Challenge exports now preserve the curve explicitly for reimports. Legacy
structured exports without a curve retain Standard scoring for compatibility.
No database schema migration is required. Rebuild the application to deploy.
