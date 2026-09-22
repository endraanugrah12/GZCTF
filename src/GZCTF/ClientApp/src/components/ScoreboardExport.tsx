import { Button, Group, Stack, Text } from '@mantine/core'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { downloadBlob } from '@Utils/ApiHelper'
import { useUser } from '@Hooks/useUser'
import api, { Role } from '@Api'

export const ScoreboardExport = ({ gameId }: { gameId: number }) => {
  const { user } = useUser()
  const { t } = useTranslation()
  const [busy, setBusy] = useState(false)
  if (user?.role !== Role.Admin && user?.role !== Role.Monitor) return null
  const download = (format: 'xlsx' | 'csv') =>
    downloadBlob(
      api.instance.get(`/api/game/${gameId}/ScoreboardSheet`, { params: { format }, responseType: 'blob' }),
      setBusy,
      t,
      `Scoreboard_${gameId}_${Date.now()}.${format}`
    )
  return (
    <Stack gap={4}>
      <Text size="xs" c="dimmed">
        Export full live scoreboard
      </Text>
      <Group gap="xs">
        <Button size="xs" disabled={busy} onClick={() => void download('xlsx')}>
          Excel (.xlsx)
        </Button>
        <Button size="xs" variant="light" disabled={busy} onClick={() => void download('csv')}>
          CSV (.csv)
        </Button>
      </Group>
    </Stack>
  )
}
