import Link from 'next/link';
import { redirect } from 'next/navigation';

interface PageProps {
  params: Promise<{ lang: string }>;
}

export default async function LangHomePage({ params }: PageProps) {
  const { lang } = await params;

  // Redirect to docs for the selected language
  redirect(`/${lang}/docs`);
}
