import {
  defineConfig,
  defineDocs,
  frontmatterSchema,
  metaSchema,
} from 'fumadocs-mdx/config';

// You can customise Zod schemas for frontmatter and `meta.json` here
// see https://fumadocs.dev/docs/mdx/collections

// Unity SDK docs for English
export const unityDocsEn = defineDocs({
  dir: 'content/docs/unity/en',
  docs: {
    schema: frontmatterSchema,
    postprocess: {
      includeProcessedMarkdown: true,
    },
  },
  meta: {
    schema: metaSchema,
  },
});

// Unity SDK docs for Chinese
export const unityDocsZh = defineDocs({
  dir: 'content/docs/unity/zh',
  docs: {
    schema: frontmatterSchema,
    postprocess: {
      includeProcessedMarkdown: true,
    },
  },
  meta: {
    schema: metaSchema,
  },
});

// Godot SDK docs for English
export const godotDocsEn = defineDocs({
  dir: 'content/docs/godot/en',
  docs: {
    schema: frontmatterSchema,
    postprocess: {
      includeProcessedMarkdown: true,
    },
  },
  meta: {
    schema: metaSchema,
  },
});

// Godot SDK docs for Chinese
export const godotDocsZh = defineDocs({
  dir: 'content/docs/godot/zh',
  docs: {
    schema: frontmatterSchema,
    postprocess: {
      includeProcessedMarkdown: true,
    },
  },
  meta: {
    schema: metaSchema,
  },
});

// Unreal SDK docs for English
export const unrealDocsEn = defineDocs({
  dir: 'content/docs/unreal/en',
  docs: {
    schema: frontmatterSchema,
    postprocess: {
      includeProcessedMarkdown: true,
    },
  },
  meta: {
    schema: metaSchema,
  },
});

// Unreal SDK docs for Chinese
export const unrealDocsZh = defineDocs({
  dir: 'content/docs/unreal/zh',
  docs: {
    schema: frontmatterSchema,
    postprocess: {
      includeProcessedMarkdown: true,
    },
  },
  meta: {
    schema: metaSchema,
  },
});

export default defineConfig({
  mdxOptions: {
    // MDX options
  },
});
