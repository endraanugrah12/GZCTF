import assert from 'node:assert/strict'
import test from 'node:test'
import { normalizeAllowedLinkHosts } from './AllowedLinkHosts'

test('normalizes complete domain names without removing internal dots', () => {
  assert.deepEqual(normalizeAllowedLinkHosts('ChatGPT.com\ngemini.google.com'), ['chatgpt.com', 'gemini.google.com'])
})

test('accepts comma and whitespace separators and removes duplicates', () => {
  assert.deepEqual(normalizeAllowedLinkHosts('claude.ai, CLAUDE.AI  perplexity.ai.'), ['claude.ai', 'perplexity.ai'])
})
