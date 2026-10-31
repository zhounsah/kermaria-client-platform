import Link from "next/link";
import type { PublicCommercialService } from "@kermaria/shared";
import { getBillingV2FormulesCatalog } from "@/lib/internal-api";
import { buildPublicCommercialCatalog } from "@/lib/public-commercial-catalog";
import styles from "./AssistancePages.module.css";

const GROUPS = {
  particuliers: {
    label: "Particuliers",
    title: "Une assistance pour vos besoins au quotidien",
    description: "Découvrez les abonnements d’assistance et de maintenance proposés aux particuliers.",
    codes: ["ASSIST-PERS-ESSENTIEL", "ASSIST-PERS-SERENITE", "ASSIST-PERS-PREMIUM"],
  },
  professionnels: {
    label: "Professionnels",
    title: "Un accompagnement adapté à votre activité",
    description: "Découvrez les abonnements d’assistance et de maintenance proposés aux professionnels.",
    codes: ["ASSIST-PRO-START", "ASSIST-PRO-BUSINESS", "ASSIST-PRO-PREMIUM"],
  },
} as const;

export type AssistanceAudience = keyof typeof GROUPS;

const BENEFITS: Readonly<Record<string, readonly string[]>> = {
  "ASSIST-PERS-ESSENTIEL": [
    "1 ordinateur couvert",
    "Diagnostic initial à distance",
    "15 minutes d’assistance à distance par mois",
    "Conseils et recommandations de sécurité",
    "10 % de remise sur les interventions hors forfait",
  ],
  "ASSIST-PERS-SERENITE": [
    "Jusqu’à 2 ordinateurs couverts",
    "45 minutes d’assistance à distance par mois",
    "Vérification trimestrielle de sécurité",
    "Contrôle des mises à jour et des sauvegardes",
    "15 % de remise sur les interventions hors forfait",
  ],
  "ASSIST-PERS-PREMIUM": [
    "Jusqu’à 4 ordinateurs du foyer",
    "90 minutes d’assistance à distance par mois",
    "Suivi préventif mensuel",
    "Vérification des sauvegardes et de l’antivirus",
    "Une visite préventive annuelle à domicile de 60 minutes",
    "Traitement prioritaire dans les horaires de support convenus",
  ],
  "ASSIST-PRO-START": [
    "Inventaire du matériel",
    "Supervision de l’état des postes",
    "Alertes de sécurité et maintenance préventive",
    "15 minutes d’assistance à distance par poste et par mois",
  ],
  "ASSIST-PRO-BUSINESS": [
    "Les prestations Pro Start",
    "Maintenance préventive renforcée",
    "45 minutes d’assistance à distance par poste et par mois",
    "Contrôles réguliers de la protection et des sauvegardes existantes",
    "Rapport mensuel synthétique",
    "Prise en charge prioritaire dans les horaires convenus",
  ],
  "ASSIST-PRO-PREMIUM": [
    "Les prestations Pro Business",
    "90 minutes d’assistance à distance par poste et par mois",
    "Suivi renforcé de la sécurité et des incidents",
    "Revue technique trimestrielle",
    "Accompagnement infrastructure et continuité d’activité",
    "Priorité renforcée selon contrat",
  ],
};

export function AssistanceHub() {
  return <div className={styles.page}>
    <div className={styles.hero}>
      <p className={styles.eyebrow}>Assistance &amp; maintenance</p>
      <h1>Une aide informatique pour vous accompagner</h1>
      <p>Découvrez les abonnements proposés selon votre situation et consultez les informations disponibles pour chaque formule.</p>
    </div>
    <section aria-labelledby="audience-title" className={styles.section}>
      <h2 id="audience-title">À qui s’adresse votre recherche ?</h2>
      <div className={styles.audiences}>
        {(Object.keys(GROUPS) as AssistanceAudience[]).map((key) => <article className={styles.audience} key={key}>
          <p className={styles.eyebrow}>{GROUPS[key].label}</p>
          <h3>{GROUPS[key].title}</h3><p>{GROUPS[key].description}</p>
          <Link className="button" href={`/assistance-maintenance/${key}`}>Voir les abonnements</Link>
        </article>)}
      </div>
    </section>
    <div className={styles.contact}><h2>Une question sur votre besoin ?</h2><p>Échangeons pour préciser le périmètre adapté à votre situation.</p><Link className="button button-secondary" href="/contact">Nous contacter</Link></div>
  </div>;
}

export async function AssistanceAudiencePage({ audience }: { audience: AssistanceAudience }) {
  const group = GROUPS[audience];
  const result = await getBillingV2FormulesCatalog();
  const catalog = buildPublicCommercialCatalog(result.data);
  return <div className={styles.page}>
    <nav aria-label="Fil d’Ariane" className={styles.breadcrumb}><Link href="/">Accueil</Link><span aria-hidden="true"> / </span><Link href="/assistance-maintenance">Assistance &amp; maintenance</Link><span aria-hidden="true"> / </span><span aria-current="page">{group.label}</span></nav>
    <div className={styles.hero}><p className={styles.eyebrow}>Assistance &amp; maintenance · {group.label}</p><h1>{group.title}</h1><p>{group.description}</p></div>
    <section aria-labelledby="subscriptions-title" className={styles.section}>
      <h2 id="subscriptions-title">Les abonnements</h2>
      <p>Le détail du périmètre et les conditions sont précisés avant toute souscription.</p>
      <div className={styles.cards}>{group.codes.map((code) => {
        const service = catalog.services.find((item) => item.id === code);
        return service ? <SubscriptionCard audience={audience} key={code} service={service} /> : <article className={styles.card} key={code}><h3>{fallbackName(code)}</h3><p>Informations momentanément indisponibles.</p><Link href="/contact">Nous contacter</Link></article>;
      })}</div>
      <p className={styles.note}>Les minutes d’assistance ne se reportent pas d’un mois sur l’autre. Licences, stockage de sauvegarde, projets et interventions hors forfait sont exclus, sauf mention explicite. Le périmètre, les outils nécessaires et les conditions d’intervention sont confirmés avant souscription.</p>
      {audience === "professionnels" ? <p className={styles.note}>Montants HT indicatifs pour une opération française taxable, selon les données du catalogue. Le devis final dépend de la qualification fiscale du client.</p> : null}
    </section>
    <div className={styles.contact}><h2>Besoin d’éclaircir un point ?</h2><p>Nous pouvons préciser les conditions et le périmètre de l’abonnement qui vous intéresse.</p><Link className="button button-secondary" href="/contact">Parler de mon besoin</Link></div>
  </div>;
}

function SubscriptionCard({ audience, service }: { audience: AssistanceAudience; service: PublicCommercialService }) {
  const price = service.startingPrice;
  const amount = audience === "professionnels" ? price?.frenchNetAmountCents : price?.amountCents;
  const available = amount !== undefined && amount !== null && price !== null;
  const directUrl = `/tarifs#ajouter-${service.id.toLowerCase().replaceAll(/[^a-z0-9]+/g, "-").replaceAll(/^-|-$/g, "")}`;
  return <article className={styles.card}>
    <p className={styles.eyebrow}>Abonnement</p><h3>{fallbackName(service.id)}</h3>
    {service.description ? <p>{service.description}</p> : null}
    <p className={styles.price}>{available ? <>{service.priceType === "from" ? "À partir de " : ""}{new Intl.NumberFormat("fr-FR", { style: "currency", currency: price.currency }).format(amount / 100)} <span>{audience === "professionnels" ? "HT indicatif / poste / mois" : "TTC / mois"}</span></> : "Tarif indisponible"}</p>
    <details className={styles.benefits}>
      <summary>Ce que prévoit la formule</summary>
      <ul>{(BENEFITS[service.id] ?? []).map((benefit) => <li key={benefit}>{benefit}</li>)}</ul>
    </details>
    {service.initialFees.length > 0 ? <p className={styles.note}>Des frais de mise en service peuvent s’appliquer. Consultez les tarifs pour leur détail.</p> : null}
    {service.directlyOrderable ? <Link className="button" href={directUrl}>Voir les modalités de commande</Link> : service.orderingMode === "offer_component" ? <Link className="button" href={service.primaryCta.href}>{service.primaryCta.label}</Link> : <Link className="button button-secondary" href="/contact">Nous contacter</Link>}
  </article>;
}

function fallbackName(code: string) {
  const suffix = code.split("-").at(-1);
  const name = suffix === "SERENITE" ? "Sérénité" : suffix === "ESSENTIEL" ? "Essentiel" : suffix === "START" ? "Start" : suffix === "BUSINESS" ? "Business" : "Premium";
  return code.startsWith("ASSIST-PRO-") ? `Pro ${name}` : name;
}
