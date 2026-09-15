import { Menu } from '@mantine/core'
import { ReactElement } from 'react'
import { useTranslation } from 'react-i18next'

/** Both challenge build entry points must explicitly offer the same cache modes. */
export function BuildModeMenu({
  children,
  onBuild,
}: {
  children: ReactElement
  onBuild: (noCache: boolean) => Promise<void>
}) {
  const { t } = useTranslation()
  return (
    <Menu withinPortal position="bottom-end">
      <Menu.Target>{children}</Menu.Target>
      <Menu.Dropdown>
        <Menu.Label>{t('admin.build_mode.title')}</Menu.Label>
        <Menu.Item onClick={() => void onBuild(false)}>{t('admin.build_mode.cached')}</Menu.Item>
        <Menu.Item color="orange" onClick={() => void onBuild(true)}>
          {t('admin.build_mode.fresh')}
        </Menu.Item>
        <Menu.Label>{t('admin.build_mode.description')}</Menu.Label>
      </Menu.Dropdown>
    </Menu>
  )
}
