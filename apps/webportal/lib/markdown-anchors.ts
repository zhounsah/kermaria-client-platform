export type MarkdownHeadingAnchor = {
  level: 1 | 2 | 3 | 4 | 5 | 6;
  title: string;
  id: string;
  line: number;
};

export function getMarkdownHeadingAnchors(markdown: string): MarkdownHeadingAnchor[] {
  const seen = new Map<string, number>();
  const anchors: MarkdownHeadingAnchor[] = [];
  const lines = markdown.split(/\r?\n/);

  lines.forEach((line, index) => {
    const match = line.match(/^(#{1,6})\s+(.+?)\s*#*\s*$/);
    if (!match) {
      return;
    }

    const title = normalizeHeadingText(match[2]);
    const base = slugifyHeading(title);
    const count = seen.get(base) ?? 0;
    seen.set(base, count + 1);
    anchors.push({
      level: match[1].length as MarkdownHeadingAnchor["level"],
      title,
      id: count === 0 ? base : `${base}-${count + 1}`,
      line: index + 1,
    });
  });

  return anchors;
}

export function getMarkdownHeadingAnchorMap(markdown: string) {
  return new Map(
    getMarkdownHeadingAnchors(markdown).map((heading) => [
      heading.line,
      heading.id,
    ]),
  );
}

export function slugifyHeading(value: string): string {
  const slug = value
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");

  return slug || "section";
}

function normalizeHeadingText(value: string): string {
  return value
    .replace(/\\([\\`*_[\]{}()#+\-.!>])/g, "$1")
    .replace(/[`*_~[\]]/g, "")
    .replace(/\s+/g, " ")
    .trim();
}
