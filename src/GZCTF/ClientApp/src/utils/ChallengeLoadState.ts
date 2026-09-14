// Scoreboard rank is intentionally irrelevant: hidden and unranked teams can play.
export const getChallengeLoadState = (
  teamInfo: { challenges?: unknown; rank?: unknown } | undefined,
  error?: unknown
): 'loading' | 'error' | 'ready' => error ? 'error' : teamInfo?.challenges ? 'ready' : 'loading'
