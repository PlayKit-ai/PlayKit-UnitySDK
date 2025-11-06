import { defineI18n } from 'fumadocs-core/i18n';

export const i18n = defineI18n({
  defaultLanguage: 'en',
  languages: ['en', 'zh'],
  // Don't hide locale prefix for better SEO and clarity
  hideLocale: 'never',
});

export const languageLabels: Record<string, string> = {
  en: 'English',
  zh: '中文',
};
