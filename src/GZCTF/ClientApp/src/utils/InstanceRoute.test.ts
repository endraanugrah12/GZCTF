import assert from 'node:assert/strict'
import test from 'node:test'
import { getPublicHttpEntry, getTcpCommand } from './InstanceRoute'

test('formats copyable nc commands with separate host and port arguments', () => {
  assert.equal(getTcpCommand('pwn.example.com:32784'), 'nc pwn.example.com 32784')
  assert.equal(getTcpCommand('127.0.0.1:5000'), 'nc 127.0.0.1 5000')
  assert.equal(getTcpCommand('[::1]:5000'), 'nc ::1 5000')
  assert.equal(getTcpCommand(''), '')
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
