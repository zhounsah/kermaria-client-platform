import Link from "next/link";

import { DisabledActionNotice } from "@/components/DisabledActionNotice";
import { PageHeader } from "@/components/PageHeader";
import { PasswordChangeForm } from "@/components/PasswordChangeForm";
import { StatusBadge } from "@/components/StatusBadge";
import { requireClientSession } from "@/lib/auth";

export const metadata = {
  title: "Mot de passe",
};

export const dynamic = "force-dynamic";

function isPasswordChangeEnabled() {
  return (
    process.env.AD_PASSWORD_CHANGE_ENABLED?.trim().toLowerCase() === "true"
  );
}

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
            ? "Modifier le mot de passe du portail et synchroniser l'identité liée lorsqu'un compte Active Directory existe."
            : "Le changement de mot de passe n'est pas disponible dans cette version."
        }
        eyebrow="Sécurité du compte"
        title="Changer mon mot de passe"
      />

      {enabled ? (
        <div className="password-layout">
          <section className="content-panel">
            <h2>Mot de passe du compte</h2>
            <p className="page-description">
              La modification est transmise a l&apos;API interne, qui verifie
              d&apos;abord le mot de passe actuel cote portail. Si votre compte
              est déjà relié à une identité AD, le nouveau secret est aussi
              synchronisé vers `clients.home.bzh`. Aucun mot de passe n&apos;est
              journalise.
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
              <li>Vérification de l'identité et de la session.</li>
              <li>Traitement par l'API interne privée uniquement.</li>
              <li>Le portail reste la source de verification du mot de passe actuel.</li>
              <li>Synchronisation AD effectuée seulement si un lien AD existe.</li>
              <li>Aucun mot de passe dans les logs.</li>
              <li>Limite de tentatives (3 / 15 min) avant verrouillage temporaire.</li>
              <li>Journal d'audit sans donnée sensible.</li>
            </ul>
          </aside>
        </div>
      ) : (
        <>
          <DisabledActionNotice
            description="Aucun mot de passe ne peut être saisi ou transmis depuis cette page. L'intégration Active Directory réelle reste désactivée."
            title="Le changement de mot de passe n'est pas disponible dans cette version."
          />

          <div className="password-layout">
            <section className="content-panel">
              <h2>Accès au portail</h2>
              <p className="page-description">
                Le compte actuellement connecte utilise l&apos;authentification
                locale du portail. Aucun parcours de modification ou de
                récupération automatisée n&apos;est active.
              </p>
              <div className="form-footer">
                <Link className="text-link" href="/profile">
                  Retour au profil
                </Link>
              </div>
            </section>

            <aside className="content-panel">
              <h2>Garanties conservees</h2>
              <ul className="check-list">
                <li>Vérification de l'identité et de la session.</li>
                <li>Traitement par l'API interne privée uniquement.</li>
                <li>Aucun mot de passe dans les logs.</li>
                <li>Journal d'audit sans donnée sensible.</li>
                <li>Aucune communication Active Directory réelle.</li>
              </ul>
            </aside>
          </div>
        </>
      )}
    </>
  );
}
