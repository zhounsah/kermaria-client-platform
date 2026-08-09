import type { EditorialContentType } from "@kermaria/shared";

export function contentTypeFromSegment(segment: string): EditorialContentType | null {
  if (segment === "wiki") return "wiki_article";
  if (segment === "seo") return "seo_page";
  if (segment === "faq") return "faq";
  return null;
}

export function editorialSectionTitle(contentType: EditorialContentType) {
  if (contentType === "wiki_article") return "Wiki";
  if (contentType === "seo_page") return "Pages SEO";
  return "FAQ";
}

export function contentTypeSegment(contentType: EditorialContentType) {
  if (contentType === "wiki_article") return "wiki";
  if (contentType === "seo_page") return "seo";
  return "faq";
}
