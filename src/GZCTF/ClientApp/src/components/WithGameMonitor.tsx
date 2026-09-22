import { Group, LoadingOverlay, Stack, Tabs } from '@mantine/core'
import { mdiExclamationThick, mdiFileDocumentCheckOutline, mdiFlag, mdiLightningBolt, mdiPackageVariant, mdiGhost } from '@mdi/js'
import { Icon } from '@mdi/react'
import React, { FC, useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useLocation, useNavigate, useParams } from 'react-router'
import { ScoreboardExport } from '@Components/ScoreboardExport'
import { WithGameTab } from '@Components/WithGameTab'
import { WithNavBar } from '@Components/WithNavbar'
import { WithRole } from '@Components/WithRole'
import { DEFAULT_LOADING_OVERLAY } from '@Utils/Shared'
import { Role } from '@Api'
import misc from '@Styles/Misc.module.css'

interface WithGameMonitorProps extends React.PropsWithChildren {
  isLoading?: boolean
}

export const WithGameMonitor: FC<WithGameMonitorProps> = ({ children, isLoading }) => {
  const { id } = useParams()
  const numId = parseInt(id ?? '-1')

  const navigate = useNavigate()
  const location = useLocation()
  const { t } = useTranslation()

  const pages = [
    { icon: mdiLightningBolt, title: t('game.tab.monitor.events'), path: 'events' },
    { icon: mdiFlag, title: t('game.tab.monitor.submissions'), path: 'submissions' },
    { icon: mdiFileDocumentCheckOutline, title: t('game.tab.monitor.evidence', 'Evidence'), path: 'evidence' },
    { icon: mdiGhost, title: t('game.tab.monitor.cheat'), path: 'CheatCheck' },
    { icon: mdiPackageVariant, title: t('game.tab.monitor.traffic'), path: 'traffic' },
  ]

  const getTab = (path: string) => pages.find((page) => path.endsWith(page.path))

  const [activeTab, setActiveTab] = useState(getTab(location.pathname)?.path ?? pages[0].path)

  useEffect(() => {
    const tab = getTab(location.pathname)
    if (tab) {
      setActiveTab(tab.path ?? '')
    } else {
      navigate(pages[0].path)
    }
  }, [location])


  return (
    <WithNavBar width="90%">
      <WithRole requiredRole={Role.Monitor}>
        <WithGameTab>
          <Group justify="space-between" align="flex-start">
            <Stack>
              <ScoreboardExport gameId={numId} />
              <Tabs
                orientation="vertical"
                value={activeTab}
                onChange={(value) => value && navigate(`/games/${id}/monitor/${value}`)}
                classNames={{
                  root: misc.w10rem,
                  list: misc.w10rem,
                }}
              >
                <Tabs.List>
                  {pages.map((page) => (
                    <Tabs.Tab key={page.path} leftSection={<Icon path={page.icon} size={1} />} value={page.path}>
                      {page.title}
                    </Tabs.Tab>
                  ))}
                </Tabs.List>
              </Tabs>
            </Stack>
            <Stack w="calc(100% - 11rem)" pos="relative">
              <LoadingOverlay visible={isLoading ?? false} overlayProps={DEFAULT_LOADING_OVERLAY} />
              {children}
            </Stack>
          </Group>
        </WithGameTab>
      </WithRole>
    </WithNavBar>
  )
}
