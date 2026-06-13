import Link from "next/link";

import { DisabledActionNotice } from "@/components/DisabledActionNotice";
import { MockNotice } from "@/components/MockNotice";
import { PageHeader } from "@/components/PageHeader";
import { StatusBadge } from "@/components/StatusBadge";
import { requirePortalSession } from "@/lib/auth";
import { getAdHealth } from "@/lib/internal-api";

export const metadata = {
  title: "Mot de passe",
};

export const dynamic = "force-dynamic";

export default async function PasswordPage() {
  await requirePortalSession();
  const adHealth = await getAdHealth();
  const modeLabel = `Mode AD : ${adHealth.data.mode}`;

  return (
    <>
      <PageHeader
        action={<StatusBadge label={modeLabel} tone="info" />}
        description="Le changement de mot de passe n’est pas disponible dans cette version."
        eyebrow="Sécurité du compte"
        title="Changer mon mot de passe"
      />

      <DisabledActionNotice
        description="Aucun mot de passe ne peut être saisi, envoyé, stocké, journalisé ou persisté. Les opérations restent désactivées même lorsque la configuration de test est contrôlée."
        title="Le changement de mot de passe n’est pas disponible dans cette version."
      />

      <div className="password-layout">
        <section className="form-card disabled-form">
          <label>
            Mot de passe actuel
            <input
              autoComplete="current-password"
              disabled
              placeholder="Saisie indisponible"
              type="password"
            />
          </label>
          <label>
            Nouveau mot de passe
            <input
              autoComplete="new-password"
              disabled
              placeholder="Saisie indisponible"
              type="password"
            />
          </label>
          <label>
            Confirmer le nouveau mot de passe
            <input
              autoComplete="new-password"
              disabled
              placeholder="Saisie indisponible"
              type="password"
            />
          </label>
          <div className="form-footer">
            <Link className="text-link" href="/profile">
              Retour au profil
            </Link>
            <button className="button" disabled type="button">
              Action désactivée
            </button>
          </div>
        </section>

        <aside className="content-panel">
          <h2>Parcours cible, non actif</h2>
          <ul className="check-list">
            <li>Vérification de l&apos;identité et de la session.</li>
            <li>Ancien mot de passe requis.</li>
            <li>Traitement par l&apos;API interne privée uniquement.</li>
            <li>Aucun mot de passe dans les logs ou la base.</li>
            <li>Journal d&apos;audit sans donnée sensible.</li>
          </ul>
        </aside>
      </div>

      <MockNotice
        correlationId={adHealth.correlationId}
        source={adHealth.source}
      />
    </>
  );
}
