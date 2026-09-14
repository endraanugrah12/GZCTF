import assert from 'node:assert/strict'
import test from 'node:test'
import { getChallengeLoadState } from './ChallengeLoadState'

test('hidden admin with rank zero is ready when challenge data has arrived', () => {
  assert.equal(getChallengeLoadState({ challenges: { Pwn: [{ id: 1 }] }, rank: { rank: 0 } }), 'ready')
})
test('unranked and ordinary teams use the same data readiness rule', () => {
  assert.equal(getChallengeLoadState({ challenges: {}, rank: { rank: 1 } }), 'ready')
  assert.equal(getChallengeLoadState({ challenges: {} }), 'ready')
  assert.equal(getChallengeLoadState(undefined), 'loading')
})
test('request failure is an error rather than an indefinite spinner', () => {
  assert.equal(getChallengeLoadState(undefined, new Error('Forbidden')), 'error')
})
