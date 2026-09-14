import { Alert, Button, Group, Stack, Switch, Text } from '@mantine/core'
import { useState } from 'react'
import { Link } from 'react-router'
import useSWR from 'swr'
import { useUser } from '@Hooks/useUser'
import api, { Role } from '@Api'

export function AdminTestingControls({ gameId, accepted }: { gameId: number; accepted: boolean }) {
  const { user } = useUser()
  const isAdmin = user?.role === Role.Admin
  const url = isAdmin && user?.userId ? `/api/admin/Users/${user.userId}` : null
  const { data, error, mutate } = useSWR<{ hideFromScoreboard: boolean }>(
    url,
    async (path: string) => (await api.instance.get(path)).data
  )
  const [busy, setBusy] = useState(false)
  const [failed, setFailed] = useState(false)
  if (!isAdmin) return null
  const toggle = async (hidden: boolean) => {
    setBusy(true)
    setFailed(false)
    try {
      await api.instance.put(url!, { hideFromScoreboard: hidden })
      await mutate({ hideFromScoreboard: hidden }, { revalidate: false })
    } catch {
      setFailed(true)
    } finally {
      setBusy(false)
    }
  }
  return (
    <Alert title="Admin preview and testing" m="md">
      <Stack gap="xs">
        <Text size="sm">
          You can view hidden games and test enabled challenges before the scheduled start. Join with an accepted test
          team first. Pre-start flag checks retain evidence but do not award solves or bloods, or send public
          notifications.
        </Text>
        <Switch
          label="Hide my account's teams from scoreboards (all games)"
          checked={data?.hideFromScoreboard ?? false}
          disabled={!data || busy}
          onChange={(e) => toggle(e.currentTarget.checked)}
        />
        <Text size="xs">
          This hides every team containing your account, including frozen standings. Use a dedicated test team;
          submissions and evidence remain stored.
        </Text>
        {(error || failed) && (
          <Text c="red" size="sm">
            Could not load or save scoreboard visibility.
          </Text>
        )}
        <Group>
          <Button component={Link} to={`/games/${gameId}/challenges`} disabled={!accepted}>
            Test challenges
          </Button>
          <Button component={Link} to={`/admin/games/${gameId}/challenges`} variant="light">
            Manage challenges
          </Button>
        </Group>
      </Stack>
    </Alert>
  )
}
