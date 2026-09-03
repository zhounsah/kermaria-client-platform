-- Repairs CP850-decoded UTF-8 sequences persisted in public CMS copy.
SET NAMES utf8mb4;

-- statement-break

UPDATE managed_content_entries SET title = REPLACE(title, CONVERT(0xE2949CC2AE USING utf8mb4), CONVERT(0xC3A9 USING utf8mb4)), body_markdown = REPLACE(body_markdown, CONVERT(0xE2949CC2AE USING utf8mb4), CONVERT(0xC3A9 USING utf8mb4));

-- statement-break

UPDATE managed_content_entries SET title = REPLACE(title, CONVERT(0xE2949CC2AC USING utf8mb4), CONVERT(0xC3AA USING utf8mb4)), body_markdown = REPLACE(body_markdown, CONVERT(0xE2949CC2AC USING utf8mb4), CONVERT(0xC3AA USING utf8mb4));

-- statement-break

UPDATE managed_content_entries SET title = REPLACE(title, CONVERT(0xE2949CC3A1 USING utf8mb4), CONVERT(0xC3A0 USING utf8mb4)), body_markdown = REPLACE(body_markdown, CONVERT(0xE2949CC3A1 USING utf8mb4), CONVERT(0xC3A0 USING utf8mb4));

-- statement-break

UPDATE managed_content_entries SET title = REPLACE(title, CONVERT(0xE294BCC3B4 USING utf8mb4), CONVERT(0xC593 USING utf8mb4)), body_markdown = REPLACE(body_markdown, CONVERT(0xE294BCC3B4 USING utf8mb4), CONVERT(0xC593 USING utf8mb4));

-- statement-break

UPDATE managed_content_entries SET title = REPLACE(title, CONVERT(0xE2949CC2BF USING utf8mb4), CONVERT(0xC3A8 USING utf8mb4)), body_markdown = REPLACE(body_markdown, CONVERT(0xE2949CC2BF USING utf8mb4), CONVERT(0xC3A8 USING utf8mb4));

-- statement-break

UPDATE public_pack_catalog_content SET content_json = REPLACE(content_json, CONVERT(0xE2949CC2AE USING utf8mb4), CONVERT(0xC3A9 USING utf8mb4));

-- statement-break

UPDATE public_pack_catalog_content SET content_json = REPLACE(content_json, CONVERT(0xE2949CC2AC USING utf8mb4), CONVERT(0xC3AA USING utf8mb4));

-- statement-break

UPDATE public_pack_catalog_content SET content_json = REPLACE(content_json, CONVERT(0xE2949CC3A1 USING utf8mb4), CONVERT(0xC3A0 USING utf8mb4));

-- statement-break

UPDATE public_pack_catalog_content SET content_json = REPLACE(content_json, CONVERT(0xE294BCC3B4 USING utf8mb4), CONVERT(0xC593 USING utf8mb4));

-- statement-break

UPDATE public_pack_catalog_content SET content_json = REPLACE(content_json, CONVERT(0xE2949CC2BF USING utf8mb4), CONVERT(0xC3A8 USING utf8mb4));
