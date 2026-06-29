import type { MetadataRoute } from "next";

import { isVitrinePublicEnabled } from "@/lib/public-routes";

export default function robots(): MetadataRoute.Robots {
  if (!isVitrinePublicEnabled()) {
    return {
      rules: {
        userAgent: "*",
        disallow: "/",
      },
    };
  }

  return {
    rules: {
      userAgent: "*",
      allow: "/",
      disallow: [
        "/admin",
        "/api",
        "/dashboard",
        "/invoices",
        "/notifications",
        "/password",
        "/profile",
        "/services",
        "/support",
        "/commercial-documents",
        "/request-service",
        "/login",
        "/signup/verify",
        "/access-denied",
      ],
    },
  };
}
