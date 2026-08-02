export const getPublicHttpEntry = (
  instanceEntry: string,
  challengeBaseDomain: string | null | undefined,
  isWebChallenge: boolean
): string | null => {
  if (!isWebChallenge || !instanceEntry || !challengeBaseDomain) return null

  const host = instanceEntry.split(':', 1)[0].toLowerCase()
  const baseDomain = challengeBaseDomain.replace(/^\.+|\.+$/g, '').toLowerCase()

  if (!baseDomain || !host.endsWith(`.${baseDomain}`)) return null
  return `https://${host}`
}
