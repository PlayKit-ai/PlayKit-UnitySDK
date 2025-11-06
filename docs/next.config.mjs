import nextra from 'nextra'

const withNextra = nextra({
  defaultShowCopyCode: true
})

export default withNextra({
  i18n: {
    locales: ['en-US', 'zh-CN'],
    defaultLocale: 'zh-CN'
  },
  reactStrictMode: true
})
