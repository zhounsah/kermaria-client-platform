"use client";

import { useRouter } from "next/navigation";
import { useRef, useState } from "react";

import { requestBffJson } from "@/lib/client-api";

export function LogoutButton() {
  const router = useRouter();
  const isSubmittingRef = useRef(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function logout() {
    if (isSubmittingRef.current) {
      return;
    }

    isSubmittingRef.current = true;
    setIsSubmitting(true);

    try {
      await requestBffJson<{ authenticated: false }>(
        "/api/auth/logout",
        { method: "POST" },
      );
    } finally {
      router.replace("/login");
      router.refresh();
    }
  }

  return (
    <button
      className="logout-button"
      disabled={isSubmitting}
      onClick={logout}
      type="button"
    >
      {isSubmitting ? "Déconnexion..." : "Déconnexion"}
    </button>
  );
}
