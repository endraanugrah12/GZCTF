export type ScoringCurve = 'Standard' | 'Linear' | 'Logarithmic' | 'CTFd'

/** Mirrors GameChallenge.CalculateChallengeScore. Values are base points, before blood bonuses. */
export function challengeScore(
  initial: number,
  minimumRate: number,
  decay: number,
  solves: number,
  curve: ScoringCurve
): number {
  if (curve === 'CTFd')
    return Math.ceil(
      Math.max(
        initial * minimumRate,
        initial + (initial * minimumRate - initial) * (Math.max(0, solves - 1) / Math.max(1, decay)) ** 2
      )
    )
  if (solves <= 1) return initial
  const factor =
    curve === 'Linear'
      ? Math.max(minimumRate, 1 - (1 - minimumRate) * ((solves - 1) / decay))
      : curve === 'Logarithmic'
        ? minimumRate + (1 - minimumRate) / (1 + Math.log(solves) / decay)
        : minimumRate + (1 - minimumRate) * Math.exp((1 - solves) / decay)
  return Math.floor(initial * factor)
}
