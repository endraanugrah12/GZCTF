import assert from 'node:assert/strict'
import test from 'node:test'
import { invitationExportCsv, parseInvitationCsv } from './TeamInvitations'

test('invitation CSV requires only email and team_name, with either column order', () => {
  assert.deepEqual(parseInvitationCsv('email,team_name\nleader@example.com,Team Alpha'), [
    { email: 'leader@example.com', teamName: 'Team Alpha' },
  ])
  assert.deepEqual(parseInvitationCsv('team_name,email\nTeam Alpha,leader@example.com'), [
    { email: 'leader@example.com', teamName: 'Team Alpha' },
  ])
})
test('CSV handles BOM, CRLF, whitespace, escaped quotes, commas and quoted newlines', () => {
  assert.deepEqual(parseInvitationCsv('\uFEFFemail,team_name\r\na@b.com,"Team, ""A"""\r\n\r\nb@b.com,"Team\nB"'), [
    { email: 'a@b.com', teamName: 'Team, "A"' },
    { email: 'b@b.com', teamName: 'Team\nB' },
  ])
})
test('CSV rejects credential columns, missing data and invalid email', () => {
  for (const csv of [
    'email,team_name,password\na@b.com,Team,secret',
    'email,team_name',
    'email,team_name\na@b.com,',
    'email,team_name\ninvalid,Team',
    'email,team_name\na@b.com,Team,extra',
  ])
    assert.throws(() => parseInvitationCsv(csv))
})
test('CSV rejects duplicate emails and teams ignoring case', () => {
  assert.throws(() => parseInvitationCsv('email,team_name\na@b.com,One\nA@B.COM,Two'))
  assert.throws(() => parseInvitationCsv('email,team_name\na@b.com,One\nb@b.com,one'))
})
test('CSV rejects malformed quotes and excessive row counts', () => {
  assert.throws(() => parseInvitationCsv('email,team_name\na@b.com,"Team'))
  assert.throws(() => parseInvitationCsv('email,team_name\na@b.com,"Team"x'))
  assert.throws(() =>
    parseInvitationCsv('email,team_name\n' + Array.from({ length: 501 }, (_, i) => `a${i}@b.com,Team ${i}`).join('\n'))
  )
})

test('export contains leader email, team, matching link and expiration', () => {
  const csv = invitationExportCsv(
    [{ email: 'leader@example.com', teamName: 'Team, "A"', token: 'abc_123', expiresAt: '2026-10-01T00:00:00Z' }],
    'https://ctf.example.com'
  )
  assert.equal(
    csv,
    'email,team_name,invitation_link,expires_at\n' +
      '"leader@example.com","Team, ""A""","https://ctf.example.com/account/register#invitation=abc_123","2026-10-01T00:00:00.000Z"'
  )
})

test('export neutralizes spreadsheet formulas', () => {
  const csv = invitationExportCsv(
    [{ email: '=cmd@example.com', teamName: '+malicious', token: 'token', expiresAt: 0 }],
    'https://ctf.example.com'
  )
  assert.match(csv, /"'=cmd@example.com"/)
  assert.match(csv, /"'\+malicious"/)
  assert.match(csv, /"1970-01-01T00:00:00.000Z"/)
})

test('batch export handles API Unix-millisecond dates and preserves each matching link', () => {
  const rows = JSON.parse(
    JSON.stringify([
      { email: 'one@example.com', teamName: 'One', token: 'token-one', expiresAt: Date.parse('2026-10-01T00:00:00Z') },
      { email: 'two@example.com', teamName: 'Two', token: 'token-two', expiresAt: Date.parse('2026-10-02T00:00:00Z') },
    ])
  )
  assert.equal(
    invitationExportCsv(rows, 'https://ctf.example.com'),
    [
      'email,team_name,invitation_link,expires_at',
      '"one@example.com","One","https://ctf.example.com/account/register#invitation=token-one","2026-10-01T00:00:00.000Z"',
      '"two@example.com","Two","https://ctf.example.com/account/register#invitation=token-two","2026-10-02T00:00:00.000Z"',
    ].join('\n')
  )
})
