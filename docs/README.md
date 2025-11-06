# Developerworks Unity SDK Documentation

This is the documentation website for Developerworks Unity SDK, built with [Next.js](https://nextjs.org/) and [Nextra](https://nextra.site/).

## Features

- 📚 Comprehensive SDK documentation
- 🌍 Bilingual support (Chinese & English)
- 🎨 Clean and modern UI with Nextra theme
- 🔍 Full-text search
- 📱 Mobile responsive

## Getting Started

### Prerequisites

- Node.js 18+
- pnpm (recommended) or npm

### Installation

```bash
cd docs
pnpm install
```

### Development

Run the development server:

```bash
pnpm run dev
```

Open [http://localhost:3000](http://localhost:3000) in your browser.

### Build

Build the documentation for production:

```bash
pnpm run build
```

### Start Production Server

```bash
pnpm run start
```

## Documentation Structure

```
docs/
├── pages/                    # Documentation pages
│   ├── index.zh-CN.mdx      # Chinese homepage
│   ├── index.en-US.mdx      # English homepage
│   └── unity/               # Unity SDK docs
│       ├── index.zh-CN.mdx
│       ├── index.en-US.mdx
│       ├── getting-started.zh-CN.mdx
│       ├── getting-started.en-US.mdx
│       └── ...
├── theme.config.jsx         # Nextra theme configuration
├── next.config.mjs          # Next.js configuration
└── package.json
```

## Adding New Pages

1. Create a new `.mdx` file in the `pages` directory
2. Add both Chinese (`.zh-CN.mdx`) and English (`.en-US.mdx`) versions
3. Update the `_meta.json` file in the same directory to add navigation

Example:

```json
{
  "new-page": {
    "title": "New Page Title"
  }
}
```

## Learn More

- [Next.js Documentation](https://nextjs.org/docs)
- [Nextra Documentation](https://nextra.site/)
- [MDX Documentation](https://mdxjs.com/)
