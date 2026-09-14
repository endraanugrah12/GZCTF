import assert from 'node:assert/strict'
import test from 'node:test'
import { getPublicHttpEntry, getInstanceDisplayEntry } from './InstanceRoute'

test('displays and copies TCP endpoints as host:port without an nc command', () => {
  assert.equal(getInstanceDisplayEntry('pwn.example.com:32784'), 'pwn.example.com:32784')
  assert.equal(getInstanceDisplayEntry('127.0.0.1:5000'), '127.0.0.1:5000')
  assert.equal(getInstanceDisplayEntry('[::1]:5000'), '[::1]:5000')
  assert.equal(getInstanceDisplayEntry(''), '')
})

test('preserves configured HTTP and WebSocket proxy endpoints', () => {
  assert.equal(getInstanceDisplayEntry('127.0.0.1:5000', 'https://web.example.com'), 'https://web.example.com')
  assert.equal(getInstanceDisplayEntry('wss://ctf.example.com/proxy/token'), 'wss://ctf.example.com/proxy/token')
})

test('removes the NodePort when the public HTTP route is enabled', () => {
  assert.equal(
    getPublicHttpEntry('source-view-c9-t4.chall.ctf.hackitbraw.site:32040', 'chall.ctf.hackitbraw.site', true),
    'https://source-view-c9-t4.chall.ctf.hackitbraw.site'
  )
})

test('preserves raw TCP entries by declining a public HTTP route', () => {
  assert.equal(
    getPublicHttpEntry('pwn-c10-t4.chall.ctf.hackitbraw.site:32041', 'chall.ctf.hackitbraw.site', false),
    null
  )
})

test('does not rewrite entries outside the configured wildcard domain', () => {
  assert.equal(getPublicHttpEntry('worker.example.net:32040', 'chall.ctf.example.com', true), null)
})
