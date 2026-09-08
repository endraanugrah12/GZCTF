import { ActionIcon, Anchor, Button, Divider, Group, Stack, Text, TextInput, Tooltip } from '@mantine/core'
import { useClipboard } from '@mantine/hooks'
import { useDebouncedCallback, useDebouncedState } from '@mantine/hooks'
import { showNotification } from '@mantine/notifications'
import {
  mdiCheck,
  mdiContentCopy,
  mdiExclamation,
  mdiOpenInNew,
  mdiServerNetwork,
  mdiTransitConnectionVariant,
} from '@mdi/js'
import { Icon } from '@mdi/react'
import { WsrxState } from '@xdsec/wsrx'
import dayjs from 'dayjs'
import duration from 'dayjs/plugin/duration'
import { FC, useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { HandleWsrxError, useWsrx } from '@Components/WsrxProvider'
import { getProxyUrl as getProxyEntry } from '@Utils/Shared'
import { useConfig } from '@Hooks/useConfig'
import { useTicker } from '@Hooks/useTicker'
import api, { ClientFlagContext, ContainerPortMappingType } from '@Api'
import { getPublicHttpEntry, getTcpCommand } from '@Utils/InstanceRoute'
import classes from '@Styles/InstanceEntry.module.css'
import misc from '@Styles/Misc.module.css'

dayjs.extend(duration)

interface InstanceEntryProps {
  readinessUrl?: string
  test?: boolean
  label?: string
  usePublicHttpRoute?: boolean
  context: ClientFlagContext
  disabled?: boolean
  onCreate?: () => void
  onExtend?: () => void
  onDestroy?: () => void
}

interface CountdownProps {
  time?: number | null
  onTimeout?: () => void
  extendEnabled: boolean
  enableExtend: () => void
}

const Countdown: FC<CountdownProps> = (props) => {
  const { time, onTimeout, extendEnabled, enableExtend } = props
  const { config } = useConfig()
  const now = useTicker()
  const [timeoutExecuted, setTimeoutExecuted] = useState(false)
  const end = time ? dayjs(time) : now.add(config.defaultLifetime ?? 120, 'minutes')

  const countdown = dayjs.duration(end.diff(now))

  useEffect(() => {
    if (!extendEnabled && config.renewalWindow && countdown.asMinutes() < config.renewalWindow) enableExtend()

    const isExpired = countdown.asSeconds() <= 0
    if (isExpired && !timeoutExecuted && onTimeout) {
      setTimeoutExecuted(true)
      onTimeout()
    }

    if (!isExpired && timeoutExecuted) {
      setTimeoutExecuted(false)
    }
  }, [countdown, config.renewalWindow, timeoutExecuted, onTimeout])

  return (
    <Text span fw="bold">
      {countdown.asSeconds() > 0 ? countdown.format('HH:mm:ss') : '00:00:00'}
    </Text>
  )
}

export const InstanceEntry: FC<InstanceEntryProps> = (props) => {
  const { test: isPreview, label, context, disabled, onCreate, onDestroy } = props
  const { wsrx, wsrxState, wsrxOptions } = useWsrx()

  const { config } = useConfig()
  const clipBoard = useClipboard()

  const [forceShowOriginal, setForceShowOriginal] = useState(false)
  const [withContainer, setWithContainer] = useState(!!context.instanceEntry)
  const [readiness, setReadiness] = useState<'starting' | 'ready' | 'timeout'>('starting')
  const [readinessAttempt, setReadinessAttempt] = useState(0)

  useEffect(() => {
    if (!context.instanceEntry || !props.readinessUrl) return
    const controller = new AbortController()
    let timer: ReturnType<typeof setTimeout> | undefined
    const deadline = Date.now() + 20_000
    setReadiness('starting')
    const check = async () => {
      try {
        const response = await api.instance.get<{ ready: boolean }>(props.readinessUrl!, {
          signal: controller.signal, timeout: 2500,
        })
        if (controller.signal.aborted) return
        if (response.data.ready) {
          setReadiness('ready')
          return
        }
      } catch {
        if (controller.signal.aborted) return
      }
      if (Date.now() >= deadline) setReadiness('timeout')
      else timer = setTimeout(check, 500)
    }
    void check()
    return () => {
      controller.abort()
      clearTimeout(timer)
    }
  }, [context.instanceEntry, props.readinessUrl, readinessAttempt])

  const waitingForService = !!props.readinessUrl && readiness !== 'ready'

  // Shared container: one container serves every team. Players can start/extend it but not
  // destroy it (admin-only), and on idle-expiry we just flip back to the start view locally.
  const isShared = context.isSharedInstance ?? false

  const instanceEntry = context.instanceEntry ?? ''
  const isPlatformProxy =
    config.portMapping === ContainerPortMappingType.PlatformProxy &&
    instanceEntry.length === 36 &&
    !instanceEntry.includes(':')
  const originalEntry = isPlatformProxy ? getProxyEntry(instanceEntry, isPreview) : instanceEntry

  const [canExtend, setCanExtend] = useDebouncedState(false, 500)

  const { t } = useTranslation()

  const enableExtend = useDebouncedCallback(() => {
    showNotification({
      color: 'orange',
      title: t('challenge.notification.instance.extend.note.title'),
      message: t('challenge.notification.instance.extend.note.message'),
      icon: <Icon path={mdiExclamation} size={1} />,
    })
    setCanExtend(true)
  }, 100)

  useEffect(() => {
    setWithContainer(!!context.instanceEntry)
    const countdown = dayjs.duration(dayjs(context.closeTime ?? 0).diff(dayjs()))
    setCanExtend(countdown.asMinutes() < (config.renewalWindow ?? 10))
  }, [context, config.renewalWindow])

  const onExtend = async () => {
    if (!canExtend || !props.onExtend) return

    try {
      await Promise.resolve(props.onExtend())

      showNotification({
        color: 'teal',
        title: t('challenge.notification.instance.extend.success.title'),
        message: t('challenge.notification.instance.extend.success.message'),
        icon: <Icon path={mdiCheck} size={1} />,
      })

      setCanExtend(false)
    } catch (err) {
      showNotification({
        color: 'red',
        title: t('challenge.notification.instance.extend.note.title'),
        message: (err as Error)?.message ?? t('common.error.unknown', 'An unknown error occurred'),
        icon: <Icon path={mdiExclamation} size={1} />,
      })
    }
  }

  const localTraffic = wsrx.list().find((traffic) => traffic.remote === originalEntry)
  const [localEntry, setLocalEntry] = useState(localTraffic?.local ?? '')

  // is wsrx is ready to use
  const isWsrxUsable = isPlatformProxy && wsrxState === WsrxState.Usable
  // to show original entry
  const useOriginal = !!localTraffic && forceShowOriginal

  useEffect(() => {
    if (!originalEntry || !isWsrxUsable) return

    const localAddr = wsrxOptions.allowLan ? '0.0.0.0:0' : '127.0.0.1:0'

    const requestProxy = async () => {
      try {
        const traffic = await wsrx.add({
          label,
          remote: originalEntry,
          local: localAddr,
        })
        setLocalEntry(traffic.local)
      } catch (err) {
        HandleWsrxError(err, t)
      }
    }

    requestProxy()
  }, [originalEntry, isWsrxUsable, label, wsrxOptions.allowLan])

  const useLocal = isWsrxUsable && !useOriginal
  const entry = useLocal ? localEntry : originalEntry
  const entryIsWss = isPlatformProxy && !useLocal
  const publicHttpEntry = !useLocal
    ? getPublicHttpEntry(entry, config.challengeBaseDomain, props.usePublicHttpRoute ?? false)
    : null
  // Directly published instances are TCP by default. Displaying the complete
  // command removes ambiguity for Pwn users and makes the copy action useful.
  const displayEntry = publicHttpEntry ?? (entryIsWss ? entry : getTcpCommand(entry))
  const webEntry = publicHttpEntry
    ?? `http://${useLocal && wsrxOptions.allowLan ? entry.replace('0.0.0.0', '127.0.0.1') : entry}`

  const onCopyEntry = () => {
    clipBoard.copy(displayEntry)

    showNotification({
      color: 'teal',
      title: entryIsWss ? t('challenge.notification.instance.copied.url.title') : undefined,
      message: entryIsWss
        ? t('challenge.notification.instance.copied.url.message')
        : t('challenge.notification.instance.copied.entry'),
      icon: <Icon path={mdiCheck} size={1} />,
    })
  }

  if (!withContainer) {
    return isPreview ? (
      <Text size="md" c="dimmed" fw="bold" pt={30}>
        {t('challenge.content.instance.test.no_container')}
      </Text>
    ) : (
      <Group justify="space-between" wrap="nowrap">
        <Stack align="left" gap={0}>
          <Text size="sm" fw="bold">
            {t('challenge.content.instance.no_container.message')}
          </Text>
          <Text size="xs" c="dimmed" fw="bold">
            {t('challenge.content.instance.no_container.note', {
              min: config.defaultLifetime,
            })}
          </Text>
        </Stack>

        <Button onClick={onCreate} disabled={disabled} loading={disabled}>
          {t('challenge.button.instance.create')}
        </Button>
      </Group>
    )
  }

  return (
    <Stack gap="sm" w="100%">
      {waitingForService && (
        <Group justify="space-between">
          <Text size="sm" c={readiness === 'timeout' ? 'orange' : 'dimmed'}>
            {readiness === 'timeout'
              ? 'The container started, but its service is not ready yet. Retry the check in a moment.'
              : 'Starting service — checking the connection…'}
          </Text>
          {readiness === 'timeout' && (
            <Button size="xs" onClick={() => setReadinessAttempt((value) => value + 1)}>Retry check</Button>
          )}
        </Group>
      )}
      <TextInput
        label={
          <Text size="sm" fw="bold">
            {t('challenge.content.instance.entry.label')}
          </Text>
        }
        description={
          isPlatformProxy &&
          !isPreview && (
            <Text span size="sm">
              {t('challenge.content.instance.entry.description.proxy')}
              &nbsp;
              <Anchor href="https://github.com/XDSEC/WebSocketReflectorX/releases" target="_blank" rel="noreferrer">
                {t('challenge.content.instance.entry.description.anchor')}
              </Anchor>
            </Text>
          )
        }
        leftSection={
          <Icon
            path={mdiServerNetwork}
            size={1}
            data-proxied={(isWsrxUsable && !useOriginal) || undefined}
            className={classes.icon}
          />
        }
        value={waitingForService ? 'Waiting for service…' : displayEntry}
        readOnly
        classNames={{ input: misc.ffmono }}
        rightSection={
          <Group gap={2}>
            <Divider orientation="vertical" pr={4} />
            {isWsrxUsable && (
              <Tooltip
                label={
                  forceShowOriginal
                    ? t('challenge.button.instance.show.proxied')
                    : t('challenge.button.instance.show.original')
                }
                withArrow
              >
                <ActionIcon
                  aria-label={
                    forceShowOriginal
                      ? t('challenge.button.instance.show.proxied')
                      : t('challenge.button.instance.show.original')
                  }
                  onClick={() => setForceShowOriginal((prev) => !prev)}
                >
                  <Icon path={mdiTransitConnectionVariant} size={1} />
                </ActionIcon>
              </Tooltip>
            )}
            <Tooltip label={t('common.button.copy')} withArrow>
              <ActionIcon disabled={waitingForService} aria-label={t('common.button.copy')} onClick={onCopyEntry}>
                <Icon path={mdiContentCopy} size={1} />
              </ActionIcon>
            </Tooltip>
            {(props.usePublicHttpRoute || isWsrxUsable) && (
              <Tooltip label={t('challenge.content.instance.open.web')} withArrow>
                <ActionIcon
                  aria-label={t('challenge.content.instance.open.web')}
                  disabled={entryIsWss || waitingForService}
                  component="a"
                  href={entryIsWss || waitingForService ? undefined : webEntry}
                  target={entryIsWss ? undefined : '_blank'}
                  rel="noreferrer"
                >
                  <Icon path={mdiOpenInNew} size={1} />
                </ActionIcon>
              </Tooltip>
            )}
          </Group>
        }
        rightSectionWidth={isWsrxUsable ? '6.5rem' : props.usePublicHttpRoute ? '5rem' : '3rem'}
      />
      {!isPreview && (
        <Group justify="space-between" wrap="nowrap">
          <Stack align="left" gap={0}>
            <Text size="sm" fw={600}>
              {t('challenge.content.instance.actions.count_down')}
              <Countdown
                time={context.closeTime}
                extendEnabled={canExtend}
                enableExtend={enableExtend}
                onTimeout={isShared ? () => setWithContainer(false) : onDestroy}
              />
            </Text>
            <Text size="xs" c="dimmed" fw={600}>
              {isShared
                ? t('challenge.content.instance.shared.note', 'Shared by all teams — only an admin can stop it.')
                : t('challenge.content.instance.actions.note', { min: config.renewalWindow })}
            </Text>
          </Stack>
          <Group justify="right" wrap="nowrap" gap="xs">
            <Button color="orange" onClick={onExtend} disabled={!canExtend || disabled} loading={disabled}>
              {t('challenge.button.instance.extend')}
            </Button>
            {!isShared && (
              <Button color="red" onClick={onDestroy} disabled={disabled} loading={disabled}>
                {t('challenge.button.instance.destroy')}
              </Button>
            )}
          </Group>
        </Group>
      )}
    </Stack>
  )
}
