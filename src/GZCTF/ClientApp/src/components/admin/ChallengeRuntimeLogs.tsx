import { Alert, Button, Code, Group, Loader, Paper, Select, Stack, Switch, Text, Title } from '@mantine/core'
import { useState } from 'react'
import useSWR from 'swr'
import api from '@Api'

type ContainerList = {
  supported: boolean
  containers: { id: string; containerId: string; team: string; status: string }[]
}
type LogResult = {
  text: string
  truncated: boolean
  state: string
  exitCode: number
  oomKilled: boolean
  error: string
}

export function ChallengeRuntimeLogs({ gameId, challengeId }: { gameId: number; challengeId: number }) {
  const [opened, setOpened] = useState(false)
  const [selected, setSelected] = useState<string | null>(null)
  const [refresh, setRefresh] = useState(false)
  const [tail, setTail] = useState('200')
  const base = `/api/edit/Games/${gameId}/Challenges/${challengeId}/RuntimeLogs`
  const fetcher = async (url: string) => (await api.instance.get(url)).data
  const list = useSWR<ContainerList>(opened ? base : null, fetcher, { refreshInterval: opened && refresh ? 10000 : 0 })
  const current = selected ?? list.data?.containers[0]?.id
  const logs = useSWR<LogResult>(
    opened && list.data?.supported && current ? `${base}/${current}?tail=${tail}` : null,
    fetcher,
    { refreshInterval: opened && refresh ? 10000 : 0, shouldRetryOnError: false }
  )
  return (
    <Paper withBorder p="md">
      <Stack gap="sm">
        <Group justify="space-between">
          <Title order={4}>Container runtime logs</Title>
          <Button variant="light" onClick={() => setOpened(!opened)}>
            {opened ? 'Hide logs' : 'View Docker logs'}
          </Button>
        </Group>
        {opened && (
          <>
            <Text size="sm">
              Read stdout/stderr from this challenge's test, shared and player containers. Logs may contain flags or
              secrets; only game administrators can access them. Build logs are separate.
            </Text>
            {list.isLoading && <Loader size="sm" />}
            {list.error && <Alert color="red">Could not load this challenge's containers.</Alert>}
            {list.data && !list.data.supported && (
              <Alert>Docker logs are unavailable for the configured container provider.</Alert>
            )}
            {list.data?.supported && (
              <>
                <Select
                  label="Container (up to 200 newest)"
                  value={current ?? null}
                  onChange={setSelected}
                  searchable
                  data={list.data.containers.map((c) => ({
                    value: c.id,
                    label: `${c.team} — ${c.containerId.slice(0, 16)} (${c.status})`,
                  }))}
                />
                {!list.data.containers.length && (
                  <Text size="sm">
                    No retained containers. Start a test/player instance first. Deleted containers have no runtime logs.
                  </Text>
                )}
                <Group>
                  <Select
                    label="Tail lines"
                    value={tail}
                    onChange={(v) => setTail(v ?? '200')}
                    data={['50', '200', '500', '1000']}
                  />
                  <Switch
                    label="Refresh every 10 seconds"
                    checked={refresh}
                    onChange={(e) => setRefresh(e.currentTarget.checked)}
                  />
                  <Button
                    variant="light"
                    onClick={() => {
                      void list.mutate()
                      void logs.mutate()
                    }}
                  >
                    Refresh logs
                  </Button>
                </Group>
                {logs.isLoading && <Loader size="sm" />}
                {logs.error && (
                  <Alert color="red">
                    Could not read logs. The container may have been removed, Docker may be unavailable, or its logging
                    driver may not support reading.
                  </Alert>
                )}
                {logs.data && (
                  <>
                    <Text size="sm">
                      State: {logs.data.state} · Exit code: {logs.data.exitCode} · OOM killed:{' '}
                      {logs.data.oomKilled ? 'yes' : 'no'}
                    </Text>
                    {logs.data.error && <Alert color="red">{logs.data.error}</Alert>}
                    {logs.data.truncated && (
                      <Alert color="yellow">Output limited to 64 KiB. Select fewer tail lines.</Alert>
                    )}
                    <Code block style={{ maxHeight: 450, overflow: 'auto', whiteSpace: 'pre-wrap' }}>
                      {logs.data.text || '(No stdout/stderr output yet)'}
                    </Code>
                  </>
                )}
              </>
            )}
          </>
        )}
      </Stack>
    </Paper>
  )
}
