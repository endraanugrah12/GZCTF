import { Alert, Button, Group, Loader, Paper, Stack, Switch, Text, Textarea, TextInput, Title } from '@mantine/core'
import { useState } from 'react'
import useSWR from 'swr'
import api from '@Api'

type Settings = {
  enabled: boolean
  bloods: boolean
  announcements: boolean
  hints: boolean
  challenges: boolean
  cheatAlerts: boolean
  username: string
  avatarUrl: string
  anonymousTeam: string
  firstBloodTitle: string
  secondBloodTitle: string
  thirdBloodTitle: string
  bloodMessage: string
  announcementMessage: string
  hintMessage: string
  challengeMessage: string
  cheatMessage: string
  footer: string
}
const textFields: [keyof Settings, string, number][] = [
  ['username', 'Bot display name', 80],
  ['avatarUrl', 'Bot avatar HTTPS URL (optional)', 2048],
  ['anonymousTeam', 'Anonymous team label during freeze', 80],
  ['firstBloodTitle', 'First blood title', 256],
  ['secondBloodTitle', 'Second blood title', 256],
  ['thirdBloodTitle', 'Third blood title', 256],
  ['bloodMessage', 'Blood message', 3000],
  ['announcementMessage', 'Announcement message', 3000],
  ['hintMessage', 'Hint message', 3000],
  ['challengeMessage', 'New challenge message', 3000],
  ['cheatMessage', 'Cheat alert message', 3000],
  ['footer', 'Embed footer', 500],
]

export function DiscordSettingsPanel({ gameId }: { gameId: number }) {
  const url = `/api/edit/Games/${gameId}/Discord`
  const { data, error, mutate } = useSWR<Settings>(url, async (path: string) => (await api.instance.get(path)).data)
  const [draft, setDraft] = useState<Settings | null>(null)
  const [busy, setBusy] = useState(false)
  const [status, setStatus] = useState('')
  const [frozen, setFrozen] = useState(true)
  const settings = draft ?? data
  if (error) return <Alert color="red">Could not load Discord settings.</Alert>
  if (!settings) return <Loader />
  const edit = (key: keyof Settings, value: string | boolean) => {
    setDraft({ ...settings, [key]: value })
    setStatus('Unsaved Discord changes')
  }
  const save = async () => {
    setBusy(true)
    try {
      await api.instance.put(url, settings)
      await mutate(settings, false)
      setDraft(null)
      setStatus('Discord settings saved')
    } catch {
      setStatus('Save failed. Check field lengths and use an HTTPS avatar URL.')
    } finally {
      setBusy(false)
    }
  }
  const preview = settings.bloodMessage.replace(
    /\{(team|challenge|game|message|details)\}/g,
    (_, key: string) =>
      ({
        team: frozen ? settings.anonymousTeam : 'Example Team',
        challenge: 'Example Challenge',
        game: 'Example Game',
        message: '',
        details: '',
      })[key as 'team'] ?? ''
  )
  return (
    <Paper withBorder p="md">
      <Stack>
        <Title order={4}>Discord notifications and message editor</Title>
        <Text size="sm">
          Uses the webhook URL above (save the game to update that URL). These options have their own Save button.
        </Text>
        <Alert>
          From the scoreboard freeze time onward, automated team names are replaced with the anonymous label and cheat
          details are withheld. This remains active after game end until the freeze setting is cleared. No test messages
          are sent from this editor.
        </Alert>
        <Group>
          {(
            [
              ['enabled', 'Enable Discord'],
              ['bloods', 'Bloods'],
              ['announcements', 'Announcements'],
              ['hints', 'Hints'],
              ['challenges', 'New challenges'],
              ['cheatAlerts', 'Cheat alerts'],
            ] as [keyof Settings, string][]
          ).map(([key, label]) => (
            <Switch
              key={key}
              label={label}
              checked={Boolean(settings[key])}
              disabled={busy}
              onChange={(e) => edit(key, e.currentTarget.checked)}
            />
          ))}
        </Group>
        <Text size="sm">
          Placeholders: {'{team}, {challenge}, {game}, {message}, {details}'}. Blood messages support
          team/challenge/game; announcements use message; cheat alerts use team/details. Footer supports game. Blank
          message templates suppress that notice type. Player-controlled values are escaped and mentions are disabled.
        </Text>
        {textFields.map(([key, label, maxLength]) =>
          key.endsWith('Message') ? (
            <Textarea
              key={key}
              label={label}
              maxLength={maxLength}
              value={String(settings[key])}
              disabled={busy}
              onChange={(e) => edit(key, e.currentTarget.value)}
              minRows={2}
              autosize
            />
          ) : (
            <TextInput
              key={key}
              label={label}
              maxLength={maxLength}
              value={String(settings[key])}
              disabled={busy}
              onChange={(e) => edit(key, e.currentTarget.value)}
            />
          )
        )}
        <Switch
          label="Preview frozen blood message"
          checked={frozen}
          onChange={(e) => setFrozen(e.currentTarget.checked)}
        />
        <Text size="sm" style={{ whiteSpace: 'pre-wrap' }}>
          {preview || '(Message disabled)'}
        </Text>
        <Text size="xs" c="dimmed">
          Text preview with sample values; Discord renders Markdown. Templates you write manually should not contain
          real team identities.
        </Text>
        {status && <Text size="sm">{status}</Text>}
        <Button loading={busy} onClick={save}>
          Save Discord settings
        </Button>
      </Stack>
    </Paper>
  )
}
