"use client";

import type { ComponentPropsWithoutRef } from "react";
import ReactMarkdown from "react-markdown";

type ManagedMarkdownProps = {
  markdown: string;
  className?: string;
};

export function ManagedMarkdown({
  markdown,
  className = "",
}: ManagedMarkdownProps) {
  return (
    <div className={`managed-markdown ${className}`.trim()}>
      <ReactMarkdown
        components={{
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
