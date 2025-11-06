import Link from 'next/link';

export default function HomePage() {
  return (
    <div className="flex flex-col justify-center text-center flex-1 px-4">
      <h1 className="text-4xl font-bold mb-4">Developerworks Unity SDK</h1>
      <p className="text-xl text-muted-foreground mb-8">
        Build powerful Unity applications with our comprehensive SDK
      </p>
      <div className="flex gap-4 justify-center">
        <Link
          href="/en/docs"
          className="px-6 py-3 bg-primary text-primary-foreground rounded-lg font-medium hover:bg-primary/90"
        >
          Documentation (English)
        </Link>
        <Link
          href="/zh/docs"
          className="px-6 py-3 bg-primary text-primary-foreground rounded-lg font-medium hover:bg-primary/90"
        >
          文档 (中文)
        </Link>
      </div>
    </div>
  );
}
