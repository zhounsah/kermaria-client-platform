"use client";

import { usePathname } from "next/navigation";
import Link from "next/link";
import { LoadingState } from "@/components/LoadingState";
import { HOME_HERO } from "@/lib/home-hero-content";

export default function Loading() {
  const pathname = usePathname();
  if (pathname === "/") return <div className="home-loading-screen">
    <p className="home-loading-kicker">Zachary IT · Informatique professionnelle</p>
    <h1 className="home-loading-title">{HOME_HERO.title}</h1>
    <p className="home-loading-lead">{HOME_HERO.body}</p>
    <Link className="button home-loading-action" href={HOME_HERO.primary.href}>{HOME_HERO.primary.label}</Link>
  </div>;

  return (
    <div className="route-loading-shell" style={{ minHeight: "100vh" }}>
      <LoadingState
        description="Les informations de cette page sont en cours de récupération."
        title="Chargement de la page"
      />
    </div>
  );
}
