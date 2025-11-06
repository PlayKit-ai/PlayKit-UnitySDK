import { getLLMText, source } from '@/lib/source';

export const revalidate = false;

export async function GET() {
  // Get pages from both languages
  const enPages = source.en.getPages().map(getLLMText);
  const zhPages = source.zh.getPages().map(getLLMText);

  const allPages = [...enPages, ...zhPages];
  const scanned = await Promise.all(allPages);

  return new Response(scanned.join('\n\n'));
}
