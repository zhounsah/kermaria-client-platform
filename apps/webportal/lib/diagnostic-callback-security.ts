export type DiagnosticCallbackRequestLike = {
  headers: Pick<Headers, "get">;
  url: string;
};

export type CallbackRequestRejection =
  | { status: 403; code: "ORIGIN_FORBIDDEN"; message: string }
  | { status: 415; code: "UNSUPPORTED_MEDIA_TYPE"; message: string };

function isJsonContentType(value: string | null) {
  return value?.split(";", 1)[0]?.trim().toLowerCase() === "application/json";
}

function isAllowedOrigin(origin: string, requestUrl: string) {
  try {
    return new URL(origin).origin === new URL(requestUrl).origin;
  } catch {
    return false;
  }
}

/**
 * Protection navigateur complementaire au limiteur et au honeypot. La page
 * publique n'utilise pas de cookie de session, donc cette route n'emploie pas
 * le double-submit CSRF des mutations authentifiees.
 */
export function validateDiagnosticCallbackRequest(
  request: DiagnosticCallbackRequestLike,
): CallbackRequestRejection | null {
  if (!isJsonContentType(request.headers.get("content-type"))) {
    return {
      status: 415,
      code: "UNSUPPORTED_MEDIA_TYPE",
      message: "Le format de la demande doit être JSON.",
    };
  }

  const fetchSite = request.headers.get("sec-fetch-site")?.toLowerCase();
  if (fetchSite === "cross-site") {
    return {
      status: 403,
      code: "ORIGIN_FORBIDDEN",
      message: "Cette origine ne peut pas envoyer cette demande.",
    };
  }

  const origin = request.headers.get("origin");
  if (origin && !isAllowedOrigin(origin, request.url)) {
    return {
      status: 403,
      code: "ORIGIN_FORBIDDEN",
      message: "Cette origine ne peut pas envoyer cette demande.",
    };
  }

  return null;
}
