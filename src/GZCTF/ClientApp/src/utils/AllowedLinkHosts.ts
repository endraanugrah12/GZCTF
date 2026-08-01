export const normalizeAllowedLinkHosts = (value: string): string[] =>
  Array.from(
    new Set(
      value
        .split(/[\n,\s]+/)
        .map((host) => host.trim().toLowerCase().replace(/\.+$/, ''))
        .filter(Boolean)
    )
  )
