import Link from "next/link";

import { DisabledActionNotice } from "@/components/DisabledActionNotice";
import { PageHeader } from "@/components/PageHeader";
import { StatusBadge } from "@/components/StatusBadge";
import { requireClientSession } from "@/lib/auth";

export const metadata = {
  title: "Mot de passe",
};

export const dynamic = "force-dynamic";

export default async function PasswordPage() {
  await requireClientSession();

  return (
    <>
      <PageHeader
        action={<StatusBadge label="Action indisponible" tone="warning" />}
        description="Le changement de mot de passe n’est pas disponible dans cette version."
        eyebrow="Sécurité du compte"
        title="Changer mon mot de passe"
      />

      <DisabledActionNotice
        description="Aucun mot de passe ne peut être saisi ou transmis depuis cette page. L’intégration Active Directory réelle reste désactivée."
        title="Le changement de mot de passe n’est pas disponible dans cette version."
      />

      <div className="password-layout">
        <section className="content-panel">
          <h2>Accès au portail</h2>
          <p className="page-description">
            Le compte actuellement connecté utilise l’authentification locale
            du portail. Aucun parcours de modification ou de récupération
            automatisée n’est activé.
          </p>
          <div className="form-footer">
            <Link className="text-link" href="/profile">
              Retour au profil
            </Link>
          </div>
        </section>

        <aside className="content-panel">
          <h2>Garanties conservées</h2>
          <ul className="check-list">
            <li>Vérification de l&apos;identité et de la session.</li>
            <li>Traitement par l&apos;API interne privée uniquement.</li>
            <li>Aucun mot de passe dans les logs ou la base.</li>
            <li>Journal d&apos;audit sans donnée sensible.</li>
            <li>Aucune communication Active Directory réelle.</li>
          </ul>
        </aside>
      </div>
    </>
  );
}
