export const getPublicHttpEntry = (
  instanceEntry: string,
  challengeBaseDomain: string | null | undefined,
  usePublicHttpRoute: boolean
): string | null => {
  if (!usePublicHttpRoute || !instanceEntry || !challengeBaseDomain) return null

  const host = instanceEntry.split(':', 1)[0].toLowerCase()
  const baseDomain = challengeBaseDomain.replace(/^\.+|\.+$/g, '').toLowerCase()

  if (!baseDomain || !host.endsWith(`.${baseDomain}`)) return null
  return `https://${host}`
}
