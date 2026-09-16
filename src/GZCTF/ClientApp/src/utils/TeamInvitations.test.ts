import assert from 'node:assert/strict'
import test from 'node:test'
import { parseInvitationCsv } from './TeamInvitations'

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
