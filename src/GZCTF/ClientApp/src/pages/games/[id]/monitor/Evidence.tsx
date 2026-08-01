import {
  ActionIcon,
  Anchor,
  Badge,
  Group,
  Paper,
  ScrollArea,
  Stack,
  Table,
  Text,
  TextInput,
  Tooltip,
} from '@mantine/core'
import { mdiDownload, mdiMagnify, mdiOpenInNew } from '@mdi/js'
import { Icon } from '@mdi/react'
import dayjs from 'dayjs'
import { FC, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useParams } from 'react-router'
import useSWR from 'swr'
import { WithGameMonitor } from '@Components/WithGameMonitor'
import { downloadBlob } from '@Utils/ApiHelper'
import { useLanguage } from '@Utils/I18n'
import api, { SubmissionEvidenceReviewModel } from '@Api'
import tableClasses from '@Styles/Table.module.css'

const formatSize = (bytes?: number) => {
  if (!bytes || bytes < 1024) return `${bytes ?? 0} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KiB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MiB`
}

const Evidence: FC = () => {
  const { id } = useParams()
  const gameId = Number.parseInt(id ?? '-1')
  const { t } = useTranslation()
  const { locale } = useLanguage()
  const [search, setSearch] = useState('')
  const [downloading, setDownloading] = useState(false)

  const { data: evidence, error } = useSWR<SubmissionEvidenceReviewModel[]>(
    gameId > 0 ? `/api/game/${gameId}/evidence` : null,
    async (url) => (await api.instance.get<SubmissionEvidenceReviewModel[]>(url)).data,
    { refreshInterval: 15_000, revalidateOnFocus: true }
  )

  const filteredEvidence = useMemo(() => {
    const needle = search.trim().toLowerCase()
    if (!needle) return evidence ?? []
    return (evidence ?? []).filter((item) =>
      [item.challengeTitle, item.teamName, item.userName, item.solverFileName, ...(item.llmLinks ?? [])]
        .filter(Boolean)
        .some((value) => value!.toLowerCase().includes(needle))
    )
  }, [evidence, search])

  return (
    <WithGameMonitor isLoading={!evidence && !error}>
      <Stack gap="sm">
        <Group justify="space-between">
          <Stack gap={0}>
            <Text fw={600}>{t('game.title.evidence', 'Submission evidence')}</Text>
            <Text size="xs" c="dimmed">
              {t('game.evidence.private_notice', 'Private LLM links and solver files submitted with flag attempts.')}
            </Text>
          </Stack>
          <TextInput
            w={280}
            placeholder={t('game.evidence.search', 'Filter team, user, challenge, link, or file')}
            leftSection={<Icon path={mdiMagnify} size={0.8} />}
            value={search}
            onChange={(event) => setSearch(event.currentTarget.value)}
          />
        </Group>
        {error && <Text c="red">{t('game.evidence.load_failed', 'Unable to load submission evidence.')}</Text>}
        <Paper shadow="md" p="md">
          <ScrollArea offsetScrollbars h="calc(100vh - 235px)">
            <Table className={tableClasses.table} striped highlightOnHover>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>{t('common.label.time')}</Table.Th>
                  <Table.Th>{t('common.label.challenge')}</Table.Th>
                  <Table.Th>{t('common.label.team')}</Table.Th>
                  <Table.Th>{t('common.label.user')}</Table.Th>
                  <Table.Th>{t('game.evidence.llm_links', 'LLM links')}</Table.Th>
                  <Table.Th>{t('game.evidence.solver', 'Solver')}</Table.Th>
                  <Table.Th>{t('game.evidence.status', 'Attempt')}</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {filteredEvidence.map((item) => (
                  <Table.Tr key={item.id}>
                    <Table.Td>
                      <Text size="sm" ff="monospace">
                        {item.uploadedAtUtc ? dayjs(item.uploadedAtUtc).locale(locale).format('LLL') : '—'}
                      </Text>
                    </Table.Td>
                    <Table.Td>{item.challengeTitle}</Table.Td>
                    <Table.Td>{item.teamName}</Table.Td>
                    <Table.Td>{item.userName}</Table.Td>
                    <Table.Td miw={190}>
                      <Stack gap={3}>
                        {(item.llmLinks ?? []).map((link) => (
                          <Anchor key={link} href={link} target="_blank" rel="noreferrer" size="xs" lineClamp={1}>
                            <Group gap={3} wrap="nowrap">
                              <Icon path={mdiOpenInNew} size={0.65} />
                              <span>{link}</span>
                            </Group>
                          </Anchor>
                        ))}
                      </Stack>
                    </Table.Td>
                    <Table.Td miw={170}>
                      <Group gap={4} wrap="nowrap">
                        <Tooltip label={t('game.evidence.download_solver', 'Download solver')}>
                          <ActionIcon
                            variant="subtle"
                            disabled={downloading || !item.id}
                            onClick={() =>
                              downloadBlob(
                                api.instance.get(`/api/game/${gameId}/evidence/${item.id}/solver`, {
                                  responseType: 'blob',
                                }),
                                setDownloading,
                                t
                              )
                            }
                          >
                            <Icon path={mdiDownload} size={0.9} />
                          </ActionIcon>
                        </Tooltip>
                        <Stack gap={0} maw={135}>
                          <Text size="sm" lineClamp={1}>{item.solverFileName}</Text>
                          <Text size="xs" c="dimmed">{formatSize(item.solverFileSize)}</Text>
                        </Stack>
                      </Group>
                    </Table.Td>
                    <Table.Td>
                      <Badge color={item.submissionId ? 'teal' : 'yellow'} variant="light">
                        {item.submissionId
                          ? t('game.evidence.attached', { defaultValue: 'Attempt #{{id}}', id: item.submissionId })
                          : t('game.evidence.pending', 'Awaiting flag attempt')}
                      </Badge>
                    </Table.Td>
                  </Table.Tr>
                ))}
                {evidence && filteredEvidence.length === 0 && (
                  <Table.Tr>
                    <Table.Td colSpan={7}>
                      <Text ta="center" c="dimmed" py="xl">
                        {search
                          ? t('game.evidence.no_match', 'No evidence matches this filter.')
                          : t('game.evidence.empty', 'No player has submitted evidence yet.')}
                      </Text>
                    </Table.Td>
                  </Table.Tr>
                )}
              </Table.Tbody>
            </Table>
          </ScrollArea>
        </Paper>
      </Stack>
    </WithGameMonitor>
  )
}

export default Evidence
