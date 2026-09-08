import { createContext, useContext, useEffect, useState, type PropsWithChildren } from 'react'
import { useLocation } from 'react-router'
import useSWR from 'swr'
import api from '@Api'

export interface Appearance {
  title: string
  slogan: string
  primaryColor: string
  logoUrl: string
  faviconUrl: string
  homeMarkdown: string
  bannerMarkdown: string
  footerMarkdown: string
  customCss: string
}

export const defaultAppearance: Appearance = {
  title: '',
  slogan: '',
  primaryColor: '',
  logoUrl: '',
  faviconUrl: '',
  homeMarkdown: '',
  bannerMarkdown: '',
  footerMarkdown: '',
  customCss: '',
}
const Context = createContext<Appearance>(defaultAppearance)
export const useAppearance = () => useContext(Context)

export const AppearanceProvider = ({ children }: PropsWithChildren) => {
  const location = useLocation()
  const admin = /^\/admin(?:\/|$)/i.test(location.pathname)
  const account = /^\/account(?:\/|$)/i.test(location.pathname)
  const preview = new URLSearchParams(location.search).get('appearancePreview') === '1' && window.parent !== window
  const { data } = useSWR<Appearance>('/api/appearance', async (url: string) => (await api.instance.get(url)).data, {
    refreshInterval: 30_000,
  })
  const [draft, setDraft] = useState<Appearance | null>(null)
  useEffect(() => {
    if (!preview) return
    const receive = (event: MessageEvent) => {
      if (
        event.origin !== window.location.origin ||
        event.source !== window.parent ||
        event.data?.type !== 'appearance-preview'
      )
        return
      const next = { ...defaultAppearance }
      for (const key of Object.keys(next) as (keyof Appearance)[]) {
        if (typeof event.data.appearance?.[key] === 'string') next[key] = event.data.appearance[key]
      }
      setDraft(next)
    }
    window.addEventListener('message', receive)
    window.parent.postMessage({ type: 'appearance-preview-ready' }, window.location.origin)
    return () => window.removeEventListener('message', receive)
  }, [preview])
  const appearance = admin ? defaultAppearance : preview && draft ? draft : (data ?? defaultAppearance)
  useEffect(() => {
    if (admin) return
    const icon = document.querySelector<HTMLLinkElement>('link[rel="icon"]')
    const original = icon?.getAttribute('href')
    if (icon && appearance.faviconUrl) icon.setAttribute('href', appearance.faviconUrl)
    return () => {
      if (icon && original) icon.setAttribute('href', original)
    }
  }, [admin, appearance.faviconUrl])
  return (
    <Context.Provider value={appearance}>
      {!admin && !account && appearance.customCss && <style data-player-appearance>{appearance.customCss}</style>}
      {children}
    </Context.Provider>
  )
}
