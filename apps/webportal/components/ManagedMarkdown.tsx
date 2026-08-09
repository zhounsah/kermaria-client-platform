"use client";

import type { ComponentPropsWithoutRef, ReactNode } from "react";
import { useMemo } from "react";
import ReactMarkdown from "react-markdown";
import rehypeSanitize, { defaultSchema } from "rehype-sanitize";
import remarkGfm from "remark-gfm";

import { getMarkdownHeadingAnchorMap } from "@/lib/markdown-anchors";

type ManagedMarkdownProps = {
  markdown: string;
  className?: string;
  withAnchors?: boolean;
};

type MarkdownComponentProps = {
  children?: ReactNode;
  node?: {
    position?: {
      start?: {
        line?: number;
      };
    };
  };
};

export function ManagedMarkdown({
  markdown,
  className = "",
  withAnchors = false,
}: ManagedMarkdownProps) {
  const headingIds = useMemo(
    () => withAnchors ? getMarkdownHeadingAnchorMap(markdown) : new Map<number, string>(),
    [markdown, withAnchors],
  );

  function headingId(node: MarkdownComponentProps["node"]) {
    const line = node?.position?.start?.line;
    return typeof line === "number" ? headingIds.get(line) : undefined;
  }

  return (
    <div className={`managed-markdown ${className}`.trim()}>
      <ReactMarkdown
        rehypePlugins={[[rehypeSanitize, markdownSanitizeSchema]]}
        remarkPlugins={[remarkGfm]}
        skipHtml
        urlTransform={safeMarkdownUrl}
        components={{
          // Le titre editorial de la page est deja rendu en h1. Si un import
          // Markdown contient un `#`, on le descend en h2, mais un corps qui
          // commence correctement en `##` garde sa hierarchie telle quelle.
          h1: ({ children, node, ...props }: MarkdownComponentProps) => (
            <h2 {...props} id={withAnchors ? headingId(node) : undefined}>
              {children}
            </h2>
          ),
          h2: ({ children, node, ...props }: MarkdownComponentProps) => (
            <h2 {...props} id={withAnchors ? headingId(node) : undefined}>
              {children}
            </h2>
          ),
          h3: ({ children, node, ...props }: MarkdownComponentProps) => (
            <h3 {...props} id={withAnchors ? headingId(node) : undefined}>
              {children}
            </h3>
          ),
          table: ({ children }) => (
            <div className="managed-markdown-table-scroll">
              <table>{children}</table>
            </div>
          ),
          img: ({ alt, src, ...props }: ComponentPropsWithoutRef<"img">) => (
            // eslint-disable-next-line @next/next/no-img-element
            <img
              {...props}
              alt={alt ?? ""}
              loading="lazy"
              src={src}
            />
          ),
          a: ({ href, ...props }: ComponentPropsWithoutRef<"a">) => (
            <a
              {...props}
              href={href}
              rel={href?.startsWith("http") ? "noreferrer noopener" : undefined}
              target={href?.startsWith("http") ? "_blank" : undefined}
            />
          ),
        }}
      >
        {markdown}
      </ReactMarkdown>
    </div>
  );
}

const markdownSanitizeSchema = {
  ...defaultSchema,
  attributes: {
    ...defaultSchema.attributes,
    a: [
      ...(defaultSchema.attributes?.a ?? []),
      ["target"],
      ["rel"],
    ],
    img: [
      ...(defaultSchema.attributes?.img ?? []),
      ["loading"],
      ["alt"],
      ["src"],
    ],
  },
};

export function safeMarkdownUrl(value: string): string {
  const trimmed = value.trim();
  if (
    trimmed.startsWith("/")
    || trimmed.startsWith("#")
    || trimmed.startsWith("mailto:")
    || trimmed.startsWith("tel:")
  ) {
    return trimmed;
  }

  try {
    const url = new URL(trimmed);
    return url.protocol === "http:" || url.protocol === "https:"
      ? url.toString()
      : "";
  } catch {
    return "";
  }
}
