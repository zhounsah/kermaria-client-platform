import Link from "next/link";

import { DisabledActionNotice } from "@/components/DisabledActionNotice";
import { PageHeader } from "@/components/PageHeader";
import { PasswordChangeForm } from "@/components/PasswordChangeForm";
import { StatusBadge } from "@/components/StatusBadge";
import { requireClientSession } from "@/lib/auth";
import { isPasswordChangeEnabled } from "@/lib/runtime-config";

export const metadata = {
  title: "Mot de passe",
};

export const dynamic = "force-dynamic";

export default async function PasswordPage() {
  await requireClientSession();
  const enabled = isPasswordChangeEnabled();

  return (
    <>
      <PageHeader
        action={
          <StatusBadge
            label={enabled ? "Portail et identité" : "Action indisponible"}
            tone={enabled ? "info" : "warning"}
          />
        }
        description={
          enabled
            ? "Modifier le mot de passe de votre espace client et des accès associés."
            : "Le changement de mot de passe n'est pas disponible pour le moment."
        }
        eyebrow="Sécurité du compte"
        title="Changer mon mot de passe"
      />

      {enabled ? (
        <div className="password-layout">
          <section className="content-panel">
            <h2>Mot de passe du compte</h2>
            <p className="page-description">
              Votre mot de passe actuel est vérifié avant tout changement. Le
              nouveau mot de passe s&apos;applique immédiatement à votre espace
              client et aux accès qui y sont rattachés.
            </p>
            <PasswordChangeForm />
            <div className="form-footer">
              <Link className="text-link" href="/profile">
                Retour au profil
              </Link>
            </div>
          </section>

          <aside className="content-panel">
            <h2>Garanties</h2>
            <ul className="check-list">
              <li>Vérification de l&apos;identité et de la session.</li>
              <li>Traitement sur nos serveurs privés uniquement.</li>
              <li>Le mot de passe actuel est systématiquement revérifié.</li>
              <li>
                Les accès rattachés à votre compte sont mis à jour dans la même
                opération.
              </li>
              <li>Aucun mot de passe dans les logs.</li>
              <li>Limite de tentatives (3 / 15 min) avant verrouillage temporaire.</li>
              <li>Journal d&apos;audit sans donnée sensible.</li>
            </ul>
          </aside>
        </div>
      ) : (
        <>
          <DisabledActionNotice
            description="Aucun mot de passe ne peut être saisi ou transmis depuis cette page pour le moment."
            title="Le changement de mot de passe n'est pas disponible pour le moment."
          />

          <div className="password-layout">
            <section className="content-panel">
              <h2>Accès au portail</h2>
              <p className="page-description">
                Le compte actuellement connecté utilise l&apos;authentification
                locale du portail. Aucun parcours de modification ou de
                récupération automatisée n&apos;est actif.
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
                <li>Traitement sur nos serveurs privés uniquement.</li>
                <li>Aucun mot de passe dans les logs.</li>
                <li>Journal d&apos;audit sans donnée sensible.</li>
                <li>Aucun accès rattaché n&apos;est modifié.</li>
              </ul>
            </aside>
          </div>
        </>
      )}
    </>
  );
}
