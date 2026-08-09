import {
  getMarkdownHeadingAnchors,
  type MarkdownHeadingAnchor,
} from "@/lib/markdown-anchors";

export type MarkdownHeading = {
  level: 2 | 3;
  title: string;
  id: string;
};

export function extractMarkdownToc(markdown: string): MarkdownHeading[] {
  return getMarkdownHeadingAnchors(markdown)
    .filter(isTocHeading)
    .map((heading) => ({
      level: heading.level,
      title: heading.title,
      id: heading.id,
    }));
}

function isTocHeading(
  heading: MarkdownHeadingAnchor,
): heading is MarkdownHeadingAnchor & { level: 2 | 3 } {
  return heading.level === 2 || heading.level === 3;
}
