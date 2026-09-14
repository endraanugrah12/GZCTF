import {
  Alert,
  Badge,
  Button,
  Group,
  Loader,
  Paper,
  Select,
  SimpleGrid,
  Stack,
  Switch,
  Tabs,
  Text,
  Textarea,
  TextInput,
  Title,
} from '@mantine/core'
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
  const [templateKey, setTemplateKey] = useState<keyof Settings>('bloodMessage')
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
  const values: Record<string, string> = {
    team: frozen ? settings.anonymousTeam : 'Example Team',
    challenge: 'Example Challenge',
    game: 'Example Game',
    message: 'The event starts soon.',
    details: frozen ? 'Details withheld during scoreboard freeze.' : 'Example detection details',
  }
  const preview = String(settings[templateKey]).replace(
    /\{(team|challenge|game|message|details)\}/g,
    (_, key: string) => values[key] ?? ''
  )
  const field = (key: keyof Settings) => {
    const [, label, maxLength] = textFields.find(([name]) => name === key)!
    return (
      <TextInput
        key={key}
        label={label}
        maxLength={maxLength}
        value={String(settings[key])}
        disabled={busy}
        onChange={(e) => edit(key, e.currentTarget.value)}
      />
    )
  }
  return (
    <Paper withBorder p="lg" radius="md" w="100%">
      <Stack gap="md">
        <Group justify="space-between">
          <div>
            <Title order={4}>Discord notifications</Title>
            <Text size="sm" c="dimmed">
              Configure delivery and customize messages for this game.
            </Text>
          </div>
          <Switch
            label="Enable Discord"
            checked={settings.enabled}
            disabled={busy}
            onChange={(e) => edit('enabled', e.currentTarget.checked)}
          />
        </Group>
        <Text size="xs" c="dimmed">
          The webhook URL above uses the game Save button. Save notification settings separately below.
        </Text>
        <Tabs defaultValue="delivery">
          <Tabs.List>
            <Tabs.Tab value="delivery">Delivery &amp; identity</Tabs.Tab>
            <Tabs.Tab value="messages">Message editor</Tabs.Tab>
          </Tabs.List>
          <Tabs.Panel value="delivery" pt="md">
            <Stack gap="lg">
              <SimpleGrid cols={{ base: 1, md: 2 }}>
                {field('username')}
                {field('avatarUrl')}
              </SimpleGrid>
              <Stack gap="sm">
                <Text fw={600} size="sm">
                  Send notifications for
                </Text>
                <SimpleGrid cols={{ base: 1, sm: 2, lg: 3 }}>
                  {(
                    [
                      ['bloods', 'First / second / third blood'],
                      ['announcements', 'Announcements'],
                      ['hints', 'New hints'],
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
                </SimpleGrid>
              </Stack>
              <Paper withBorder p="md" radius="sm">
                <Stack gap="xs">
                  <Group>
                    <Text fw={600} size="sm">
                      Scoreboard freeze privacy
                    </Text>
                    <Badge variant="light">Always enforced</Badge>
                  </Group>
                  <Text size="sm" c="dimmed">
                    Team names become anonymous and cheat details are withheld from freeze time until the freeze is
                    cleared, including after game end.
                  </Text>
                  {field('anonymousTeam')}
                </Stack>
              </Paper>
            </Stack>
          </Tabs.Panel>
          <Tabs.Panel value="messages" pt="md">
            <SimpleGrid cols={{ base: 1, md: 2 }} spacing="lg">
              <Stack gap="sm">
                <Select
                  label="Message type"
                  value={templateKey}
                  allowDeselect={false}
                  onChange={(value) => setTemplateKey((value ?? 'bloodMessage') as keyof Settings)}
                  data={textFields
                    .filter(([key]) => key.endsWith('Message'))
                    .map(([value, label]) => ({ value, label }))}
                />
                {templateKey === 'bloodMessage' && (
                  <Stack gap="xs">
                    {field('firstBloodTitle')}
                    {field('secondBloodTitle')}
                    {field('thirdBloodTitle')}
                  </Stack>
                )}
                <Textarea
                  label="Message template"
                  description="Leave blank to disable this message type."
                  minRows={5}
                  autosize
                  maxRows={12}
                  maxLength={3000}
                  value={String(settings[templateKey])}
                  disabled={busy}
                  onChange={(e) => edit(templateKey, e.currentTarget.value)}
                />
                <Text size="xs" c="dimmed">
                  Placeholders: {'{team}, {challenge}, {game}, {message}, {details}'}. Announcements use message; cheat
                  alerts use team/details.
                </Text>
                {field('footer')}
                <Text size="xs" c="dimmed">
                  The footer supports {'{game}'}.
                </Text>
              </Stack>
              <Stack gap="sm">
                <Group justify="space-between">
                  <Text fw={600}>Preview</Text>
                  <Switch
                    label="Frozen scoreboard"
                    checked={frozen}
                    onChange={(e) => setFrozen(e.currentTarget.checked)}
                  />
                </Group>
                <Paper withBorder p="md" radius="sm" style={{ borderLeft: '4px solid var(--mantine-color-indigo-5)' }}>
                  <Stack gap="xs">
                    <Text fw={600}>{settings.username || 'GZCTF'}</Text>
                    <Text size="sm" style={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>
                      {preview || '(Message disabled)'}
                    </Text>
                    <Text size="xs" c="dimmed">
                      {settings.footer.replace(/\{game\}/g, 'Example Game')}
                    </Text>
                  </Stack>
                </Paper>
                <Text size="xs" c="dimmed">
                  Sample text preview; Discord renders Markdown. No messages are sent. Avoid manually writing team
                  identities in templates.
                </Text>
              </Stack>
            </SimpleGrid>
          </Tabs.Panel>
        </Tabs>
        <Group justify="space-between" pt="sm">
          <Text size="sm" role="status">
            {status || (draft ? 'Unsaved changes' : 'Settings up to date')}
          </Text>
          <Group>
            <Button
              variant="default"
              disabled={!draft || busy}
              onClick={() => {
                setDraft(null)
                setStatus('')
              }}
            >
              Discard changes
            </Button>
            <Button loading={busy} disabled={!draft} onClick={save}>
              Save Discord settings
            </Button>
          </Group>
        </Group>
      </Stack>
    </Paper>
  )
}
