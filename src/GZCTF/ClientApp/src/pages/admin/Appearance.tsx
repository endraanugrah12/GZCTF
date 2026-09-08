import {
  Alert,
  Button,
  ColorInput,
  Group,
  Loader,
  Modal,
  Paper,
  SimpleGrid,
  Stack,
  Text,
  Textarea,
  TextInput,
  Title,
} from '@mantine/core'
import { showNotification } from '@mantine/notifications'
import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { AdminPage } from '@Components/admin/AdminPage'
import { showErrorMsg } from '@Utils/Shared'
import { type Appearance, defaultAppearance } from '@Hooks/useAppearance'
import api from '@Api'

export default function AppearanceEditor() {
  const { t } = useTranslation()
  const [draft, setDraft] = useState<Appearance>(defaultAppearance)
  const [loaded, setLoaded] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState(false)
  const [resetOpen, setResetOpen] = useState(false)
  const [mobile, setMobile] = useState(false)
  const [message, setMessage] = useState('Changes are private until you publish.')
  const frame = useRef<HTMLIFrameElement>(null)
  const latest = useRef(draft)
  latest.current = draft
  const sendPreview = () =>
    frame.current?.contentWindow?.postMessage(
      { type: 'appearance-preview', appearance: latest.current },
      window.location.origin
    )

  useEffect(() => {
    let active = true
    api.instance
      .get<{ draft: Appearance }>('/api/appearance/editor')
      .then(({ data }) => {
        if (active) {
          setDraft({ ...defaultAppearance, ...data.draft })
          setLoaded(true)
        }
      })
      .catch((err) => {
        if (active) {
          setError(true)
          showErrorMsg(err, t)
        }
      })
    return () => {
      active = false
    }
  }, [])
  useEffect(() => {
    const timer = setTimeout(sendPreview, 250)
    return () => clearTimeout(timer)
  }, [draft, loaded])
  useEffect(() => {
    const ready = (event: MessageEvent) => {
      if (
        event.origin === window.location.origin &&
        event.source === frame.current?.contentWindow &&
        event.data?.type === 'appearance-preview-ready'
      )
        sendPreview()
    }
    window.addEventListener('message', ready)
    return () => window.removeEventListener('message', ready)
  }, [])

  const save = async (publish: boolean) => {
    setBusy(true)
    try {
      if (publish) await api.instance.post('/api/appearance/publish', draft)
      else await api.instance.put('/api/appearance/draft', draft)
      const text = publish
        ? 'Published. Player pages refresh the design within 30 seconds.'
        : 'Draft saved. Players still see the published design.'
      setMessage(text)
      showNotification({ color: 'teal', message: text })
    } catch (err) {
      showErrorMsg(err, t)
    } finally {
      setBusy(false)
    }
  }
  const reset = async () => {
    setBusy(true)
    try {
      await api.instance.delete('/api/appearance')
      setDraft(defaultAppearance)
      setMessage('Defaults restored and published. Your original Settings branding is active again.')
      setResetOpen(false)
    } catch (err) {
      showErrorMsg(err, t)
    } finally {
      setBusy(false)
    }
  }
  const field = (key: keyof Appearance, value: string) => {
    setDraft((current) => ({ ...current, [key]: value }))
    setMessage('Unpublished changes. Save a draft or publish when ready.')
  }

  return (
    <AdminPage head={<Title order={2}>Appearance editor</Title>}>
      {!loaded ? (
        error ? (
          <Alert color="red">Could not load the editor. Refresh to try again.</Alert>
        ) : (
          <Loader />
        )
      ) : (
        <Stack>
          <Alert>{message}</Alert>
          <Group>
            <Button disabled={busy} onClick={() => save(false)}>
              Save draft
            </Button>
            <Button color="teal" disabled={busy} onClick={() => save(true)}>
              Publish design
            </Button>
            <Button variant="outline" color="red" disabled={busy} onClick={() => setResetOpen(true)}>
              Restore defaults
            </Button>
          </Group>
          <SimpleGrid cols={{ base: 1, lg: 2 }}>
            <Stack>
              <Text size="sm" c="dimmed">
                Blank branding fields use your existing Settings. Markdown supports images and links. Custom CSS applies
                to player pages; admin and account pages keep their standard styles.
              </Text>
              <TextInput
                label="Site title prefix"
                description="Shown before ::CTF"
                maxLength={80}
                value={draft.title}
                onChange={(e) => field('title', e.currentTarget.value)}
              />
              <TextInput
                label="Slogan"
                maxLength={200}
                value={draft.slogan}
                onChange={(e) => field('slogan', e.currentTarget.value)}
              />
              <ColorInput
                label="Primary color"
                placeholder="#18cb9e"
                format="hex"
                value={draft.primaryColor}
                onChange={(value) => field('primaryColor', value)}
              />
              <TextInput
                label="Logo image URL"
                placeholder="https://… or /assets/…"
                maxLength={2048}
                value={draft.logoUrl}
                onChange={(e) => field('logoUrl', e.currentTarget.value)}
              />
              <TextInput
                label="Favicon URL"
                placeholder="https://… or /assets/…"
                maxLength={2048}
                value={draft.faviconUrl}
                onChange={(e) => field('faviconUrl', e.currentTarget.value)}
              />
              <Textarea
                label="Announcement banner (Markdown)"
                maxLength={3000}
                minRows={3}
                autosize
                value={draft.bannerMarkdown}
                onChange={(e) => field('bannerMarkdown', e.currentTarget.value)}
              />
              <Textarea
                label="Homepage content (Markdown)"
                description="Appears above the game and post lists. Add welcome text, rules, images, or useful links."
                maxLength={20000}
                minRows={6}
                autosize
                value={draft.homeMarkdown}
                onChange={(e) => field('homeMarkdown', e.currentTarget.value)}
              />
              <Textarea
                label="Footer (Markdown)"
                maxLength={3000}
                minRows={3}
                autosize
                value={draft.footerMarkdown}
                onChange={(e) => field('footerMarkdown', e.currentTarget.value)}
              />
              <Textarea
                label="Custom CSS"
                description="Useful selectors: .player-banner, .player-home-content, and Mantine component classes. No JavaScript."
                maxLength={20000}
                minRows={8}
                autosize
                styles={{ input: { fontFamily: 'monospace' } }}
                value={draft.customCss}
                onChange={(e) => field('customCss', e.currentTarget.value)}
              />
            </Stack>
            <Stack>
              <Group justify="space-between">
                <Text fw={600}>Live player preview</Text>
                <Button size="xs" variant="light" onClick={() => setMobile(!mobile)}>
                  {mobile ? 'Desktop width' : 'Mobile width'}
                </Button>
              </Group>
              <Paper withBorder p="xs" style={{ overflow: 'auto' }}>
                <iframe
                  ref={frame}
                  title="Player appearance preview"
                  src="/?appearancePreview=1"
                  onLoad={sendPreview}
                  style={{ border: 0, width: mobile ? 390 : '100%', height: 900, display: 'block', margin: 'auto' }}
                />
              </Paper>
              <Text size="xs" c="dimmed">
                Preview shows the actual homepage with your draft. Draft changes do not affect other visitors.
              </Text>
            </Stack>
          </SimpleGrid>
          <Modal opened={resetOpen} onClose={() => setResetOpen(false)} title="Restore default appearance?">
            <Stack>
              <Text>This clears the draft and published overrides. Branding from Settings will be used again.</Text>
              <Button color="red" loading={busy} onClick={reset}>
                Restore and publish defaults
              </Button>
            </Stack>
          </Modal>
        </Stack>
      )}
    </AdminPage>
  )
}
