import "server-only";

import type { InternalSession } from "@kermaria/shared";
import { cache } from "react";
import { redirect } from "next/navigation";

import { getInternalSession } from "@/lib/internal-api";
import { readPortalSessionToken } from "@/lib/session-cookie";

export const getCurrentPortalSession = cache(
  async (): Promise<InternalSession | null> => {
    const sessionToken = await readPortalSessionToken();

    if (!sessionToken) {
      return null;
    }

    try {
      return await getInternalSession(sessionToken);
    } catch {
      return null;
    }
  },
);

export async function requirePortalSession() {
  const session = await getCurrentPortalSession();

  if (!session) {
    redirect("/login");
  }

  return session;
}
