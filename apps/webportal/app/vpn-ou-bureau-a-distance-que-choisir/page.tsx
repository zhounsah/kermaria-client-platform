import type { Metadata } from "next";
import Link from "next/link";

import { ServiceBreadcrumb } from "@/components/PublicServiceComponents";
import { buildPublicMetadata } from "@/lib/public-metadata";
import { PUBLIC_SITE_URL } from "@/lib/public-route-config";
import { breadcrumbJsonLd, JsonLd } from "@/lib/seo";

const path = "/vpn-ou-bureau-a-distance-que-choisir";

export const metadata: Metadata = buildPublicMetadata({
  title: "VPN ou bureau Windows à distance : choisir selon vos usages",
  description: "Accès VPN ou bureau Windows à distance : comprenez ce que chaque solution permet et les points à vérifier pour votre activité avec Zachary IT.",
  path: "/vpn-ou-bureau-a-distance-que-choisir",
});

export const dynamic = "force-dynamic";

const breadcrumb = [
  { name: "Services", path: "/services" },
  { name: "Accès à distance", path },
];

export default function RemoteAccessComparisonPage() {
  return (
    <>
      <JsonLd data={breadcrumbJsonLd(PUBLIC_SITE_URL, breadcrumb)} />
      <article className="compare-guide">
        <ServiceBreadcrumb items={breadcrumb} />
        <header className="compare-guide-header">
          <h1>VPN ou bureau Windows à distance : que choisir ?</h1>
          <p>
            Ces deux solutions permettent de travailler hors des locaux, mais
            elles ne donnent pas accès aux outils de la même manière. Le bon
            choix dépend de vos logiciels, de vos fichiers et des personnes concernées.
          </p>
        </header>

        <div className="compare-guide-options">
          <section aria-labelledby="vpn-guide-title">
            <h2 id="vpn-guide-title">Accès VPN</h2>
            <p>
              Vous gardez votre poste habituel. Une connexion privée vous donne
              accès aux fichiers ou services autorisés, sans les ouvrir à tout Internet.
              Les droits des utilisateurs et les ressources accessibles sont à définir.
            </p>
            <Link className="service-inline-link" href="/services/vpn-entreprise">
              Découvrir l&apos;accès VPN
            </Link>
          </section>
          <section aria-labelledby="desktop-guide-title">
            <h2 id="desktop-guide-title">Bureau Windows à distance</h2>
            <p>
              Vous ouvrez un environnement Windows complet depuis un autre appareil.
              Les logiciels s&apos;exécutent dans ce bureau distant ; il faut vérifier
              les applications, les données et les périphériques à utiliser.
            </p>
            <Link className="service-inline-link" href="/services/bureau-windows-distance">
              Découvrir le bureau à distance
            </Link>
          </section>
        </div>

        <section className="compare-guide-questions" aria-labelledby="guide-questions-title">
          <div>
            <h2 id="guide-questions-title">Les questions à trancher avant un devis</h2>
            <p>Décrivez les usages avant de choisir une technologie ou une offre.</p>
          </div>
          <ul>
            <li>Quels logiciels doivent fonctionner à distance ?</li>
            <li>Quels fichiers et services chaque personne doit-elle retrouver ?</li>
            <li>Combien de personnes et quels appareils sont concernés ?</li>
            <li>Qui administrera les accès, les mises à jour et les sauvegardes ?</li>
          </ul>
        </section>

        <section className="compare-guide-next" aria-labelledby="guide-next-title">
          <div>
            <h2 id="guide-next-title">Parlons de votre situation</h2>
            <p>
              Un accès distant ne remplace pas la sauvegarde des données. Décrivez
              votre environnement et nous préciserons le périmètre utile.
            </p>
          </div>
          <Link className="button" href="/contact">Demander un devis</Link>
        </section>
      </article>
    </>
  );
}
