import assert from 'node:assert/strict'
import test from 'node:test'
import { challengeScore } from './ChallengeScoring'

test('CTFd decay 9 reaches minimum when all ten teams solve', () => {
  assert.deepEqual(
    Array.from({ length: 11 }, (_, n) => challengeScore(500, 0.2, 9, n, 'CTFd')),
    [500, 500, 496, 481, 456, 421, 377, 323, 259, 184, 100]
  )
})
test('CTFd decay 50 reproduces the reported 500, 500, 500, 499 pattern', () => {
  assert.deepEqual(
    [1, 2, 3, 4].map((n) => challengeScore(500, 0.2, 50, n, 'CTFd')),
    [500, 500, 500, 499]
  )
})
test('CTFd decay 10 preserves the first solve and floors at 11 solves', () => {
  assert.deepEqual(
    [0, 1, 2, 3, 10, 11, 100].map((n) => challengeScore(500, 0.2, 10, n, 'CTFd')),
    [500, 500, 496, 484, 176, 100, 100]
  )
})
test('preview responds to edits and preserves legacy curves', () => {
  assert.equal(challengeScore(1000, 0.1, 10, 2, 'CTFd'), 991)
  assert.equal(challengeScore(500, 0.2, 5, 2, 'Standard'), Math.floor(500 * (0.2 + 0.8 * Math.exp(-0.2))))
  assert.equal(challengeScore(500, 0.2, 5, 2, 'Linear'), 420)
  assert.equal(challengeScore(500, 0.2, 5, 2, 'Logarithmic'), Math.floor(500 * (0.2 + 0.8 / (1 + Math.log(2) / 5))))
})
