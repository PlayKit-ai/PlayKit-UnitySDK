import { source } from '@/lib/source';
import { createFromSource } from 'fumadocs-core/search/server';

// Create search for each language
const searchEn = createFromSource(source.en, {
  language: 'english',
});

const searchZh = createFromSource(source.zh, {
  language: 'chinese',
});

// Handle both languages
export async function GET(request: Request) {
  const url = new URL(request.url);
  const locale = url.searchParams.get('locale') || 'en';

  if (locale === 'zh') {
    return searchZh.GET(request);
  }

  return searchEn.GET(request);
}
