// Display and clipboard share the same endpoint, without adding a shell command.
export const getInstanceDisplayEntry = (entry: string, publicHttpEntry: string | null = null): string =>
  publicHttpEntry ?? entry

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
