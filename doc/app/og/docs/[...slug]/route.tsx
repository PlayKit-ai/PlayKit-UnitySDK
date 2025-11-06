import { getPageImage, source } from '@/lib/source';
import { notFound } from 'next/navigation';
import { ImageResponse } from 'next/og';
import { generate as DefaultImage } from 'fumadocs-ui/og';

export const revalidate = false;

export async function GET(
  _req: Request,
  { params }: RouteContext<'/og/docs/[...slug]'>,
) {
  const { slug } = await params;

  // Try to find the page in any language
  let page = null;
  for (const lang of ['en', 'zh'] as const) {
    page = source[lang].getPage(slug.slice(0, -1));
    if (page) break;
  }

  if (!page) notFound();

  return new ImageResponse(
    (
      <DefaultImage
        title={page.data.title}
        description={page.data.description}
        site="Developerworks Unity SDK"
      />
    ),
    {
      width: 1200,
      height: 630,
    },
  );
}

export function generateStaticParams() {
  const params: { slug: string[] }[] = [];

  for (const lang of ['en', 'zh'] as const) {
    for (const page of source[lang].getPages()) {
      params.push({
        slug: getPageImage(page).segments,
      });
    }
  }

  return params;
}
