import {
  unityDocsEn,
  unityDocsZh,
  godotDocsEn,
  godotDocsZh,
  unrealDocsEn,
  unrealDocsZh,
} from '@/.source';
import { type InferPageType, loader } from 'fumadocs-core/source';
import { i18n } from '@/lib/i18n';
import { sdkIcons } from '@/components/sdk-icons';

// See https://fumadocs.dev/docs/headless/source-api for more info
export const source = loader({
  baseUrl: '/docs',
  i18n,
  icon(icon) {
    if (icon && icon in sdkIcons) {
      return sdkIcons[icon as keyof typeof sdkIcons];
    }
  },
  source: (locale) => {
    if (locale === 'en') {
      return [
        ...unityDocsEn.toFumadocsSource(),
        ...godotDocsEn.toFumadocsSource(),
        ...unrealDocsEn.toFumadocsSource(),
      ];
    }
    // Chinese
    return [
      ...unityDocsZh.toFumadocsSource(),
      ...godotDocsZh.toFumadocsSource(),
      ...unrealDocsZh.toFumadocsSource(),
    ];
  },
});

export function getPageImage(page: InferPageType<typeof source>) {
  const segments = [...page.slugs, 'image.png'];

  return {
    segments,
    url: `/og/docs/${segments.join('/')}`,
  };
}

export async function getLLMText(page: InferPageType<typeof source>) {
  const processed = await page.data.getText('processed');

  return `# ${page.data.title}

${processed}`;
}
