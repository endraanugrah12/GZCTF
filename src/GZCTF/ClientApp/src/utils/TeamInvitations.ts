export interface InvitationRow {
  email: string
  teamName: string
}

/** Strict two-column CSV with quoted fields, CRLF, BOM and embedded newlines. */
export function parseInvitationCsv(input: string): InvitationRow[] {
  const records: string[][] = []
  let record: string[] = []
  let field = ''
  let quoted = false
  let closed = false
  const text = input.replace(/^\uFEFF/, '')
  const finishField = () => {
    record.push(field.trim())
    field = ''
    closed = false
  }
  const finishRecord = () => {
    finishField()
    if (record.some(Boolean)) records.push(record)
    record = []
  }
  for (let i = 0; i < text.length; i++) {
    const c = text[i]
    if (quoted) {
      if (c === '"' && text[i + 1] === '"') {
        field += '"'
        i++
      } else if (c === '"') {
        quoted = false
        closed = true
      } else field += c
    } else if (c === ',') finishField()
    else if (c === '\n' || c === '\r') {
      if (c === '\r' && text[i + 1] === '\n') i++
      finishRecord()
    } else if (c === '"' && !field.trim() && !closed) {
      field = ''
      quoted = true
    } else if (c === '"' || (closed && c.trim())) throw new Error('Malformed CSV quoting.')
    else field += c
  }
  if (quoted) throw new Error('Unclosed CSV quote.')
  finishRecord()
  const headers = records.shift()?.map((h) => h.toLowerCase())
  if (!headers || headers.length !== 2 || !headers.includes('email') || !headers.includes('team_name'))
    throw new Error('CSV must contain exactly these headers: email,team_name')
  if (!records.length || records.length > 500) throw new Error('Upload between 1 and 500 teams at a time.')
  const emails = new Set<string>()
  const names = new Set<string>()
  return records.map((row, i) => {
    const email = row[headers.indexOf('email')]
    const teamName = row[headers.indexOf('team_name')]
    if (
      row.length !== 2 ||
      !email ||
      !/^[^\s@]+@[^\s@]+$/.test(email) ||
      email.length > 254 ||
      !teamName ||
      teamName.length > 255
    )
      throw new Error(`Row ${i + 2}: provide a valid email and team_name.`)
    if (emails.has(email.toUpperCase()) || names.has(teamName.toUpperCase()))
      throw new Error(`Row ${i + 2}: duplicate email or team_name.`)
    emails.add(email.toUpperCase())
    names.add(teamName.toUpperCase())
    return { email, teamName }
  })
}

export function invitationLink(token: string): string {
  // Fragment is not sent in HTTP requests or referrers; send the token only in POST bodies.
  return `${window.location.origin}/account/register#invitation=${encodeURIComponent(token)}`
}

export async function invitationRequest<T>(path: string, method = 'GET', data?: unknown): Promise<T> {
  const response = await fetch(path, {
    method,
    credentials: 'same-origin',
    cache: 'no-store',
    headers: { 'Content-Type': 'application/json' },
    body: data === undefined ? undefined : JSON.stringify(data),
  })
  const body = await response.json().catch(() => null)
  if (!response.ok) throw new Error(body?.message || body?.title || `Request failed (${response.status}).`)
  return body as T
}
