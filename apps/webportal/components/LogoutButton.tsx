"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";

export function LogoutButton() {
  const router = useRouter();
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function logout() {
    setIsSubmitting(true);

    try {
      await fetch("/api/auth/logout", { method: "POST" });
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
