import { useRouter } from 'next/router'

export default {
  logo: <span>Developerworks SDK</span>,
  project: {
    link: 'https://github.com/cnqdztp/Developerworks-UnitySDK'
  },
  docsRepositoryBase: 'https://github.com/cnqdztp/Developerworks-UnitySDK/tree/main/docs',
  footer: {
    text: 'Developerworks SDK Documentation'
  },
  sidebar: {
    defaultMenuCollapseLevel: 1,
    toggleButton: true
  },
  toc: {
    backToTop: true
  },
  i18n: [
    { locale: 'en-US', text: 'English' },
    { locale: 'zh-CN', text: '简体中文' }
  ],
  useNextSeoProps() {
    const { locale } = useRouter()
    return {
      titleTemplate: locale === 'zh-CN' ? '%s – Developerworks SDK' : '%s – Developerworks SDK'
    }
  }
}
