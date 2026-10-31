import Image from "next/image";
import Link from "next/link";
import type { PublicCommercialService, PublicCommercialTier } from "@kermaria/shared";
import { getBillingV2FormulesCatalog } from "@/lib/internal-api";
import { AnimatedPublicH1, LetterWave } from "@/components/AnimatedPublicH1";
import { buildPublicCommercialCatalog } from "@/lib/public-commercial-catalog";
import {
  ASSISTANCE_AUDIENCES, assistanceCartHref, assistanceContactHref,
  assistanceDisplayPrice, assistanceOfferName, assistanceOffersFor,
  type AssistanceAudience, type AssistanceOffer,
} from "@/lib/assistance-offers";
import styles from "./AssistancePages.module.css";

export type { AssistanceAudience } from "@/lib/assistance-offers";

function money(amount: number, currency: string) {
  return new Intl.NumberFormat("fr-FR", { style: "currency", currency }).format(amount / 100);
}

export function AssistanceHomeLink() {
  return <section aria-labelledby="assistance-home-title" className={styles.homeLink}>
    <div>
      <p className={styles.eyebrow}>Assistance &amp; maintenance</p>
      <h2 id="assistance-home-title">Un suivi pour vos outils du quotidien.</h2>
      <p>Comparez les formules pour votre foyer ou les postes de votre activité.</p>
    </div>
    <Link className="button" href="/assistance-maintenance">Découvrir les abonnements <span aria-hidden="true">↗</span></Link>
  </section>;
}

export async function AssistanceHub() {
  const result = await getBillingV2FormulesCatalog();
  const catalog = buildPublicCommercialCatalog(result.data);
  return <div className={styles.page}>
    <header className={styles.hubHero}>
      <div className={styles.heroCopy}>
        <p className={styles.eyebrow}>Assistance &amp; maintenance</p>
        <h1 aria-label="Une informatique bien accompagnée."><LetterWave text="Une informatique" /><br /><span className={styles.heroAccent}><LetterWave text="bien accompagnée." /></span></h1>
        <p className={styles.lead}>Une question, un ordinateur à suivre, un parc à entretenir : choisissez l’accompagnement adapté à votre quotidien.</p>
        <div className={styles.heroActions}>
          <Link className="button" href="/assistance-maintenance/particuliers">Pour mon foyer <span aria-hidden="true">↗</span></Link>
          <Link className="button button-secondary" href="/assistance-maintenance/professionnels">Pour mon activité <span aria-hidden="true">↗</span></Link>
        </div>
        <p className={styles.heroNote}>Particuliers et professionnels · Prix et conditions vérifiés dans le panier</p>
      </div>
      <div aria-hidden="true" className={styles.heroMedia}>
        <Image alt="" height={640} priority src="/brand/network-illustration-motion.webp" width={960} />
      </div>
    </header>

    <section aria-labelledby="assistance-why-subscribe-title" className={styles.whySubscribe}>
      <div className={styles.whyIntro}>
        <p className={styles.eyebrow}>Pourquoi choisir un abonnement ?</p>
        <h2 id="assistance-why-subscribe-title">Votre informatique compte chaque jour.<br /><span>Son suivi aussi.</span></h2>
        <p>Une prestation à la carte répond à un besoin ponctuel. L’abonnement donne un cadre mensuel à votre assistance : du temps prévu, des équipements couverts et un niveau de suivi adapté à la formule.</p>
        <div className={styles.whyActions}>
          <Link className={styles.whyPrimary} href="/assistance-maintenance/particuliers">Voir les formules particuliers <span aria-hidden="true">↗</span></Link>
          <Link className={styles.whySecondary} href="/assistance-maintenance/professionnels">Voir les formules professionnels <span aria-hidden="true">↗</span></Link>
        </div>
      </div>
      <ol className={styles.whyBenefits}>
        <li><span aria-hidden="true">01</span><div><h3>Un temps d’aide déjà prévu</h3><p>15, 45 ou 90 minutes d’assistance à distance chaque mois, selon la formule choisie.</p></div></li>
        <li><span aria-hidden="true">02</span><div><h3>Un périmètre clair dès le départ</h3><p>D’un à quatre ordinateurs du foyer, ou un abonnement par poste pour votre activité.</p></div></li>
        <li><span aria-hidden="true">03</span><div><h3>Plus qu’une réponse au problème du jour</h3><p>Diagnostic, sécurité, sauvegardes, inventaire ou maintenance préventive complètent l’aide selon le niveau retenu.</p></div></li>
      </ol>
    </section>

    <section aria-labelledby="audience-title" className={styles.choiceSection}>
      <div className={styles.sectionHeading}>
        <h2 id="audience-title">À chaque usage,<br />son accompagnement.</h2>
        <p>Les formules se comparent sur des critères simples : les équipements couverts, le temps d’assistance et le suivi prévu.</p>
      </div>
      <div className={styles.audiences}>
        {(["particuliers", "professionnels"] as const).map((audience, index) => {
          const group = ASSISTANCE_AUDIENCES[audience];
          const prices = assistanceOffersFor(catalog.services, audience)
            .map(({ offer, tier }) => assistanceDisplayPrice(offer, tier))
            .filter((price) => price !== null)
            .sort((left, right) => left.amountCents - right.amountCents);
          const price = prices[0];
          return <article className={[styles.audience, audience === "professionnels" ? styles.audiencePro : ""].join(" ")} key={audience}>
            <div className={styles.audienceTop}><span className={styles.audienceIndex} aria-hidden="true">0{index + 1}</span><span>{group.label}</span></div>
            <h3>{audience === "particuliers" ? "Pour les ordinateurs du foyer." : "Pour les postes de votre activité."}</h3>
            <p className={styles.audienceCoverage}>{group.coverage}</p>
            <p className={styles.audiencePrice}>{price ? <>À partir de <strong>{money(price.amountCents, price.currency)}</strong><span>{group.unit}</span></> : "Tarifs momentanément indisponibles"}</p>
            <ul className={styles.audienceFacts}>
              <li>15, 45 ou 90 minutes d’assistance à distance{audience === "professionnels" ? " par poste" : ""} par mois</li>
              <li>{audience === "particuliers" ? "Diagnostic, sécurité et suivi selon la formule" : "Inventaire, supervision et maintenance selon la formule"}</li>
            </ul>
            <Link className={styles.audienceLink} href={"/assistance-maintenance/" + audience}>Comparer les formules {group.label.toLowerCase()} <span aria-hidden="true">↗</span></Link>
          </article>;
        })}
      </div>
    </section>

    <section className={styles.process} aria-labelledby="assistance-process-title">
      <div><p className={styles.eyebrow}>La prochaine étape</p><h2 id="assistance-process-title">Un choix clair,<br />puis votre panier.</h2></div>
      <ol>
        <li><span aria-hidden="true">01</span><div><h3>Comparez les formules</h3><p>Repérez la couverture et le temps d’assistance qui vous conviennent.</p></div></li>
        <li><span aria-hidden="true">02</span><div><h3>Choisissez la quantité</h3><p>Pour une formule professionnelle, indiquez de 1 à 10 postes. Le panier recalcule le prix.</p></div></li>
        <li><span aria-hidden="true">03</span><div><h3>Vérifiez avant paiement</h3><p>Relisez la durée, le montant TTC et les conditions dans le panier avant de confirmer.</p></div></li>
      </ol>
    </section>
    <AssistanceHelp />
  </div>;
}

function AudienceNavigation({ audience }: { audience: AssistanceAudience }) {
  return <nav aria-label="Choisir le public des abonnements" className={styles.audienceNavigation}>
    {(["particuliers", "professionnels"] as const).map((key) => <Link
      key={key} href={"/assistance-maintenance/" + key}
      aria-current={audience === key ? "page" : undefined}
    >{ASSISTANCE_AUDIENCES[key].label}</Link>)}
  </nav>;
}

export async function AssistanceAudiencePage({ audience }: { audience: AssistanceAudience }) {
  const group = ASSISTANCE_AUDIENCES[audience];
  const result = await getBillingV2FormulesCatalog();
  const catalog = buildPublicCommercialCatalog(result.data);
  const offers = assistanceOffersFor(catalog.services, audience);
  return <div className={styles.page}>
    <nav aria-label="Fil d’Ariane" className={styles.breadcrumb}>
      <Link href="/">Accueil</Link><span aria-hidden="true">/</span>
      <Link href="/assistance-maintenance">Assistance &amp; maintenance</Link><span aria-hidden="true">/</span>
      <span aria-current="page">{group.label}</span>
    </nav>
    <header className={styles.audienceHero}>
      <div><p className={styles.eyebrow}>Assistance &amp; maintenance</p><AnimatedPublicH1 text={group.title} /><p className={styles.lead}>{group.description}</p></div>
      <div className={styles.audienceControls}>
        <AudienceNavigation audience={audience} />
        <Link className={styles.jumpLink} href="#formules">Comparer les formules <span aria-hidden="true">↓</span></Link>
      </div>
    </header>

    <section id="formules" aria-labelledby="subscriptions-title" className={styles.plansSection}>
      <div className={styles.planHeading}>
        <div><h2 id="subscriptions-title">{offers.length === 3 ? "Trois niveaux de suivi." : "Les formules disponibles."}</h2><p>Comparez la couverture, l’assistance et le suivi inclus.</p></div>
        <p className={styles.quoteNote}>Votre panier reprend la formule choisie et recalcule le montant.</p>
      </div>
      {offers.length > 0 ? <>
        <nav className={styles.planJumps} aria-label="Aller à une formule">
          {offers.map(({ offer, tier }) => <a href={"#" + offer.anchor} key={offer.code}>{assistanceOfferName(offer, tier.label)}</a>)}
        </nav>
        <div className={styles.cards}>{offers.map(({ offer, service, tier }, index) => <SubscriptionCard key={offer.code} offer={offer} service={service} tier={tier} index={index} />)}</div>
      </> : <div className={styles.unavailable} role="status">
        <h3>Les formules sont momentanément indisponibles.</h3>
        <p>Nous ne pouvons pas afficher les tarifs actuels. Vous pouvez nous décrire votre besoin pour préparer un devis.</p>
        <Link className="button button-secondary" href="/contact">Être conseillé</Link>
      </div>}
      {audience === "professionnels" && offers.length > 0 ? <p className={styles.taxNote}>Prix HT indicatifs pour une opération française taxable. Le montant TTC et la TVA applicables sont vérifiés dans le panier avant paiement.</p> : null}
    </section>

    <section className={styles.conditions} aria-labelledby="assistance-conditions-title">
      <h2 id="assistance-conditions-title">À savoir avant de choisir</h2>
      <dl>
        <div><dt>Le temps d’assistance</dt><dd>Les minutes incluses sont disponibles chaque mois et ne se reportent pas au mois suivant.</dd></div>
        <div><dt>Pour les professionnels</dt><dd>Les minutes par poste sont mutualisées entre les postes couverts. Le nombre de postes est choisi dans le panier, de 1 à 10.</dd></div>
        <div><dt>La prise en charge</dt><dd>Du lundi au vendredi, de 9 h à 18 h, hors jours fériés français. Les contrôles sont planifiés ; aucune surveillance continue ni délai de résolution garanti n’est inclus.</dd></div>
        <div><dt>Les prestations hors forfait</dt><dd>Licences, stockage de sauvegarde, projets et interventions hors forfait sont exclus, sauf mention explicite dans la formule.</dd></div>
        <div><dt>La mise en service</dt><dd>Le périmètre, les outils nécessaires et les conditions d’intervention sont présentés avant paiement.</dd></div>
      </dl>
      <p>La formule Premium particuliers comprend une visite préventive annuelle de 60 minutes en Ille-et-Vilaine, déplacement inclus, sur rendez-vous. <Link href="/cgv">Consulter les conditions générales de vente</Link>.</p>
    </section>
    <AssistanceHelp />
  </div>;
}

function SubscriptionCard({ offer, service, tier, index }: { offer: AssistanceOffer; service: PublicCommercialService; tier: PublicCommercialTier; index: number }) {
  const name = assistanceOfferName(offer, tier.label);
  const price = assistanceDisplayPrice(offer, tier);
  const pro = offer.audience === "professionnels";
  const directUrl = assistanceCartHref(offer);
  return <article className={styles.card} id={offer.anchor} aria-labelledby={"title-" + offer.anchor}>
    <div className={styles.cardMain}>
      <div className={styles.cardTop}><span aria-hidden="true">0{index + 1}</span><span>{offer.cadence}</span></div>
      <header><h3 id={"title-" + offer.anchor}>{name}</h3><p className={styles.cardSummary}>{tier.description?.trim() || offer.summary}</p></header>
      <div className={styles.priceBlock}>
        <p className={styles.price}>{price ? <>{money(price.amountCents, price.currency)}<span>{ASSISTANCE_AUDIENCES[offer.audience].unit}</span></> : "Tarif indisponible"}</p>
        {pro && price && tier.recurringPrice ? <p className={styles.grossPrice}>Soit {money(tier.recurringPrice.amountCents, tier.recurringPrice.currency)} TTC / poste / mois en France</p> : null}
      </div>
      <dl className={styles.metrics}>
        <div><dt>Assistance à distance</dt><dd><strong>{offer.minutes} min</strong> / mois{pro ? " / poste" : ""}</dd></div>
        <div><dt>Couverture</dt><dd>{offer.coverage}</dd></div>
      </dl>
      <ul className={styles.highlights}>{offer.highlights.map((benefit) => <li key={benefit}><span aria-hidden="true">✓</span>{benefit}</li>)}</ul>
      <div className={styles.cardAction}>
        {service.directlyOrderable ? <Link className="button" href={directUrl}>Choisir cette formule</Link>
          : service.orderingMode === "offer_component" ? <Link className="button" href={service.primaryCta.href}>{service.primaryCta.label}</Link>
          : <Link className="button" aria-label={"Demander un devis pour " + name} href={assistanceContactHref(offer)}>Demander un devis <span aria-hidden="true">↗</span></Link>}
        {service.orderingMode === "quote" ? <p>Votre choix est repris dans le formulaire.</p> : null}
      </div>
      {service.initialFees.length > 0 || tier.initialFees.length > 0 ? <Link className={styles.feesLink} href="/tarifs">Consulter les frais de mise en service</Link> : null}
    </div>
    {offer.details.length > 0 ? <details className={styles.benefits}>
      <summary>Autres prestations<span className={styles.srOnly}> — {name}</span></summary>
      <ul>{offer.details.map((benefit) => <li key={benefit}>{benefit}</li>)}</ul>
    </details> : null}
  </article>;
}

function AssistanceHelp() {
  return <section className={styles.contact} aria-labelledby="assistance-help-title">
    <div><p className={styles.eyebrow}>Besoin d’un conseil ?</p><h2 id="assistance-help-title">Parlons de votre quotidien.</h2><p>Décrivez vos équipements et ce qui vous préoccupe. Nous préciserons ensemble le périmètre adapté.</p></div>
    <Link className="button button-secondary" href="/contact">M’aider à choisir <span aria-hidden="true">↗</span></Link>
  </section>;
}
