import {
  Alert,
  Badge,
  Button,
  FileButton,
  Group,
  Modal,
  ModalProps,
  NumberInput,
  Pagination,
  ScrollArea,
  Stack,
  Table,
  Text,
  Textarea,
} from '@mantine/core'
import { Dropzone } from '@mantine/dropzone'
import { FC, useEffect, useMemo, useState } from 'react'
import {
  downloadTextFile,
  invitationExportCsv,
  invitationLink,
  invitationRequest,
  parseInvitationCsv,
} from '@Utils/TeamInvitations'

interface Invitation {
  id: string
  email: string
  teamName: string
  expiresAt: number
  status: 'pending' | 'redeemed' | 'expired' | 'revoked'
  token: string | null
}
interface UserImportModalProps extends ModalProps {
  onImportComplete?: () => void
}

export const UserImportModal: FC<UserImportModalProps> = ({ onImportComplete, ...props }) => {
  const [csv, setCsv] = useState('')
  const [days, setDays] = useState<string | number>(30)
  const [savedDays, setSavedDays] = useState(30)
  const [rows, setRows] = useState<Invitation[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [busy, setBusy] = useState(false)
  const [loaded, setLoaded] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [newBatch, setNewBatch] = useState<Invitation[]>([])
  const base = '/api/admin/team-invitations'
  const reload = async () => {
    const list = await invitationRequest<{ items: Invitation[]; total: number }>(`${base}?page=${page}`)
    setRows(list.items)
    setTotal(list.total)
  }
  useEffect(() => {
    if (!props.opened) {
      setRows([])
      setNewBatch([])
      setLoaded(false)
      return
    }
    let active = true
    setLoaded(false)
    void Promise.all([
      invitationRequest<{ lifetimeDays: number }>(`${base}/settings`),
      invitationRequest<{ items: Invitation[]; total: number }>(`${base}?page=${page}`),
    ])
      .then(([settings, list]) => {
        if (!active) return
        setDays(settings.lifetimeDays)
        setSavedDays(settings.lifetimeDays)
        setRows(list.items)
        setTotal(list.total)
        setLoaded(true)
      })
      .catch((e: Error) => {
        if (active) setError(e.message)
      })
    return () => {
      active = false
    }
  }, [props.opened, page])
  const run = async (action: () => Promise<void>) => {
    setBusy(true)
    setError('')
    setNotice('')
    try {
      await action()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Request failed.')
    } finally {
      setBusy(false)
    }
  }
  const change = (row: Invitation, action: 'revoke' | 'regenerate') => {
    if (
      !window.confirm(
        `${action === 'revoke' ? 'Revoke' : 'Regenerate'} invitation for ${row.teamName}? The previous link will stop working.`
      )
    )
      return
    void run(async () => {
      await invitationRequest(`${base}/${row.id}/${action}`, 'POST')
      await reload()
      setNotice(
        action === 'regenerate' ? 'New invitation created. Copy and distribute the new link.' : 'Invitation revoked.'
      )
    })
  }
  const preview = useMemo(() => {
    if (!csv.trim()) return { rows: [], error: '' }
    try {
      return { rows: parseInvitationCsv(csv), error: '' }
    } catch (e) {
      return { rows: [], error: e instanceof Error ? e.message : 'Invalid CSV.' }
    }
  }, [csv])
  const loadFile = async (file: File) => {
    if (file.size > 1024 * 1024) throw new Error('CSV must be smaller than 1 MiB.')
    const text = await file.text()
    parseInvitationCsv(text)
    setCsv(text)
  }
  const downloadable = (items: Invitation[]) =>
    items.filter((item): item is Invitation & { token: string } => item.status === 'pending' && !!item.token)
  const downloadInvitations = (items: Invitation[], filename: string) => {
    const active = downloadable(items)
    if (!active.length) throw new Error('There are no active invitation links to export.')
    downloadTextFile(invitationExportCsv(active), filename)
  }
  return (
    <Modal
      {...props}
      title="Team leader invitations"
      size="xl"
      closeOnClickOutside={!busy}
      closeOnEscape={!busy}
      onClose={() => {
        if (!busy) props.onClose()
      }}
    >
      <Stack>
        <Text size="sm">
          Upload email and team_name only. Leaders choose their own username and password when they redeem a link. No
          accounts or passwords are generated on import.
        </Text>
        {error && (
          <Alert color="red" title="Could not complete request">
            {error}
          </Alert>
        )}
        {notice && <Alert color="teal">{notice}</Alert>}
        <Group align="end">
          <NumberInput
            label="Invitation lifetime (days)"
            min={1}
            max={3650}
            allowDecimal={false}
            value={days}
            onChange={setDays}
            disabled={busy || !loaded}
          />
          <Button
            disabled={busy || !loaded || typeof days !== 'number' || days < 1 || days > 3650}
            onClick={() =>
              void run(async () => {
                const result = await invitationRequest<{ lifetimeDays: number }>(`${base}/settings`, 'PUT', {
                  lifetimeDays: days,
                })
                setSavedDays(result.lifetimeDays)
                setNotice('Invitation lifetime saved.')
              })
            }
          >
            Save lifetime
          </Button>
        </Group>
        <Text size="xs" c="dimmed">
          Saved lifetime: {savedDays} days. Applies to new and regenerated invitations. Existing expiration dates do not
          change.
        </Text>
        <Dropzone
          accept={['text/csv', 'text/plain', 'application/vnd.ms-excel']}
          maxSize={1024 * 1024}
          multiple={false}
          disabled={busy}
          onDrop={(files) => {
            if (files[0]) void run(async () => loadFile(files[0]))
          }}
          onReject={() => setError('Drop one CSV file smaller than 1 MiB.')}
        >
          <Stack align="center" gap={4} py="md">
            <Text fw={600}>Drag and drop your CSV here</Text>
            <Text size="xs" c="dimmed">
              Required headers: email,team_name
            </Text>
          </Stack>
        </Dropzone>
        <Textarea
          label="CSV: email,team_name"
          autosize
          minRows={4}
          maxRows={10}
          value={csv}
          placeholder={'email,team_name\nleader@example.com,Team Alpha'}
          onChange={(e) => setCsv(e.currentTarget.value)}
          disabled={busy}
        />
        <Group>
          <FileButton
            accept=".csv,text/csv"
            onChange={(file) => {
              if (file)
                void run(async () => {
                  await loadFile(file)
                })
            }}
          >
            {(fileProps) => (
              <Button {...fileProps} variant="light" disabled={busy}>
                Upload CSV
              </Button>
            )}
          </FileButton>
          <Button
            variant="subtle"
            disabled={busy}
            onClick={() =>
              downloadTextFile('email,team_name\nleader@example.com,Team Alpha\n', 'team-invitations-template.csv')
            }
          >
            Download template
          </Button>
          <Button
            disabled={busy || !loaded || !preview.rows.length || !!preview.error || days !== savedDays}
            loading={busy}
            onClick={() =>
              void run(async () => {
                const result = await invitationRequest<{ created: number; invitations: Invitation[] }>(
                  `${base}/import`,
                  'POST',
                  { rows: preview.rows }
                )
                setNewBatch(result.invitations)
                setCsv('')
                await reload()
                onImportComplete?.()
                setNotice(`${result.created} invitations created. Copy and securely send each link to its team leader.`)
              })
            }
          >
            Create invitations
          </Button>
          {days !== savedDays && <Text size="xs">Save the lifetime before importing.</Text>}
        </Group>
        {preview.error && (
          <Alert color="orange" title="CSV validation">
            {preview.error}
          </Alert>
        )}
        {!!preview.rows.length && (
          <Stack gap={4}>
            <Text size="sm" fw={600}>
              Preview: {preview.rows.length} team{preview.rows.length === 1 ? '' : 's'}
            </Text>
            <ScrollArea h={Math.min(240, 42 + preview.rows.length * 34)}>
              <Table striped withTableBorder>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Leader email</Table.Th>
                    <Table.Th>Team name</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {preview.rows.slice(0, 50).map((row) => (
                    <Table.Tr key={`${row.email}:${row.teamName}`}>
                      <Table.Td>{row.email}</Table.Td>
                      <Table.Td>{row.teamName}</Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            </ScrollArea>
            {preview.rows.length > 50 && (
              <Text size="xs" c="dimmed">
                Showing the first 50 rows.
              </Text>
            )}
          </Stack>
        )}
        <Text size="sm">
          Pending invitations reserve team names. The account and team are created together on redemption. Treat links
          as secrets; anyone holding a link can claim that team's leader account.
        </Text>
        <Group>
          <Button
            variant="light"
            disabled={busy || !downloadable(newBatch).length}
            onClick={() => void run(async () => downloadInvitations(newBatch, 'team-invitations-new-batch.csv'))}
          >
            Download new batch CSV
          </Button>
          <Button
            variant="light"
            disabled={busy || !loaded}
            onClick={() =>
              void run(async () => {
                const active = await invitationRequest<Invitation[]>(`${base}/active`)
                downloadInvitations(active, 'team-invitations-active.csv')
              })
            }
          >
            Download all active CSV
          </Button>
        </Group>
        <ScrollArea>
          <Table miw={740}>
            <Table.Thead>
              <Table.Tr>
                <Table.Th>Email</Table.Th>
                <Table.Th>Team</Table.Th>
                <Table.Th>Status</Table.Th>
                <Table.Th>Expires</Table.Th>
                <Table.Th>Actions</Table.Th>
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {rows.map((row) => (
                <Table.Tr key={row.id}>
                  <Table.Td>{row.email}</Table.Td>
                  <Table.Td>{row.teamName}</Table.Td>
                  <Table.Td>
                    <Badge color={row.status === 'pending' ? 'blue' : row.status === 'redeemed' ? 'teal' : 'gray'}>
                      {row.status}
                    </Badge>
                  </Table.Td>
                  <Table.Td>{new Date(row.expiresAt).toLocaleString()}</Table.Td>
                  <Table.Td>
                    <Group gap="xs" wrap="nowrap">
                      <Button
                        size="xs"
                        variant="light"
                        disabled={busy || !row.token}
                        onClick={() =>
                          void run(async () => {
                            await navigator.clipboard.writeText(invitationLink(row.token!))
                            setNotice(`Link copied for ${row.teamName}.`)
                          })
                        }
                      >
                        Copy link
                      </Button>
                      <Button
                        size="xs"
                        variant="light"
                        disabled={busy || row.status === 'redeemed' || days !== savedDays}
                        onClick={() => change(row, 'regenerate')}
                      >
                        Regenerate
                      </Button>
                      <Button
                        size="xs"
                        color="red"
                        variant="subtle"
                        disabled={busy || row.status !== 'pending'}
                        onClick={() => change(row, 'revoke')}
                      >
                        Revoke
                      </Button>
                    </Group>
                  </Table.Td>
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </ScrollArea>
        {loaded && !total && (
          <Text c="dimmed" size="sm">
            No invitations yet.
          </Text>
        )}
        {total > 50 && <Pagination total={Math.ceil(total / 50)} value={page} onChange={setPage} disabled={busy} />}
      </Stack>
    </Modal>
  )
}
