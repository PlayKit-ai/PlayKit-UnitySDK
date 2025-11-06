import { unityDocsEn, unityDocsZh } from '@/.source';
import { type InferPageType, loader } from 'fumadocs-core/source';
import { lucideIconsPlugin } from 'fumadocs-core/source/lucide-icons';

// See https://fumadocs.dev/docs/headless/source-api for more info
export const source = {
  en: loader({
    baseUrl: '/docs',
    source: unityDocsEn.toFumadocsSource(),
    plugins: [lucideIconsPlugin()],
  }),
  zh: loader({
    baseUrl: '/docs',
    source: unityDocsZh.toFumadocsSource(),
    plugins: [lucideIconsPlugin()],
  }),
} as const;

export function getPageImage(page: InferPageType<typeof source.en>) {
  const segments = [...page.slugs, 'image.png'];

  return {
    segments,
    url: `/og/docs/${segments.join('/')}`,
  };
}

export async function getLLMText(page: InferPageType<typeof source.en>) {
  const processed = await page.data.getText('processed');

  return `# ${page.data.title}

${processed}`;
}
