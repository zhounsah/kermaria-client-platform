import Link from "next/link";

import {
  ServiceBreadcrumb,
  ServiceCard,
  ServiceCategoryCard,
  ServiceCTA,
  ServiceFeatureList,
  ServiceHero,
} from "@/components/PublicServiceComponents";
import { SERVICE_CATEGORIES, type ServiceCategory } from "@/lib/public-services";

export function PublicServicesLandingPage() {
  return (
    <div className="services-page">
      <ServiceHero
        action={{ href: "/contact", label: "Demander un audit" }}
        description="Zachary IT accompagne les indépendants, associations, TPE et PME qui veulent une informatique fiable sans avoir à l’administrer eux-mêmes. Nous clarifions le besoin, mettons en place les bons services et restons présents dans la durée."
        title="Une informatique fiable, sans avoir à tout gérer vous-même."
      />

      <section className="service-section" aria-labelledby="univers-title">
        <header className="service-section-heading">
          <h2 id="univers-title">Quatre univers, une même exigence.</h2>
          <p>Des services concrets, présentés par besoin plutôt que par jargon technique.</p>
        </header>
        <div className="service-category-grid">
          {SERVICE_CATEGORIES.map((category) => (
            <ServiceCategoryCard category={category} key={category.slug} />
          ))}
        </div>
      </section>

      <section className="service-section service-main-services" aria-labelledby="principaux-title">
        <header className="service-section-heading">
          <h2 id="principaux-title">Les services les plus demandés.</h2>
          <p>Chaque accompagnement est ajusté à votre contexte. Les formules existantes restent disponibles séparément, avec leurs tarifs et leur parcours propre.</p>
        </header>
        <div className="service-overview-grid">
          <article>
            <h3>Protéger ce qui compte</h3>
            <p>Sauvegarde, supervision, accès distant et continuité pour que vos fichiers et outils restent disponibles.</p>
            <Link className="service-inline-link" href="/services/cloud-hebergement">Voir le cloud & hébergement</Link>
          </article>
          <article>
            <h3>Faire circuler l’information</h3>
            <p>Domaines, e-mail, Microsoft 365 et réglages de délivrabilité gérés avec méthode.</p>
            <Link className="service-inline-link" href="/services/domaines-messagerie">Voir domaines & messagerie</Link>
          </article>
          <article>
            <h3>Travailler sereinement</h3>
            <p>Réseau, accès, postes et assistance pour que l’informatique reste un appui au quotidien.</p>
            <Link className="service-inline-link" href="/services/support-it">Voir support & IT</Link>
          </article>
        </div>
      </section>

      <section className="service-process" aria-labelledby="process-title">
        <div>
          <h2 id="process-title">Un accompagnement clair, du premier échange au suivi.</h2>
          <p>Pas de catalogue plaqué sur votre situation. Nous partons de vos usages, de vos contraintes et de ce qui doit continuer à fonctionner.</p>
        </div>
        <ol>
          <li><strong>Comprendre.</strong><span>Vous expliquez vos besoins, vos priorités et les difficultés rencontrées.</span></li>
          <li><strong>Proposer.</strong><span>Une solution lisible, avec le périmètre, les prérequis et le mode d’accompagnement.</span></li>
          <li><strong>Suivre.</strong><span>Une mise en place préparée, puis un interlocuteur pour la suite.</span></li>
        </ol>
      </section>

      <section className="service-trust" aria-labelledby="trust-title">
        <div>
          <h2 id="trust-title">Une relation de confiance, sans promesse vague.</h2>
          <p>Les services sont expliqués en langage clair, avec leurs responsabilités, leurs limites et le bon niveau de suivi.</p>
        </div>
        <ServiceFeatureList items={[
          "Un interlocuteur direct et accessible.",
          "Des recommandations proportionnées à vos usages.",
          "Des solutions conçues pour être maintenues dans la durée.",
        ]} />
      </section>

      <ServiceCTA
        action={{ href: "/contact", label: "Demander un audit" }}
        description="Décrivez simplement votre situation. Nous vous aiderons à identifier la prochaine étape la plus utile."
        title="Vous ne savez pas par où commencer ?"
      />
    </div>
  );
}

export function PublicServiceCategoryPage({ category }: { category: ServiceCategory }) {
  return (
    <div
      className="services-page service-category-page"
      id={category.slug === "support-it" ? "infogerance" : undefined}
    >
      <ServiceBreadcrumb items={[{ name: "Services", path: "/services" }, { name: category.title, path: `/services/${category.slug}` }]} />
      <ServiceHero
        action={category.cta}
        compact
        description={category.intro}
        title={category.title}
      />

      <section className="service-problem-section" aria-labelledby="problem-title">
        <div>
          <h2 id="problem-title">Ce que cet accompagnement peut résoudre.</h2>
          <p>{category.audience}</p>
        </div>
        <ServiceFeatureList items={category.problems} />
      </section>

      <section className="service-section" aria-labelledby="offers-title">
        <header className="service-section-heading">
          <h2 id="offers-title">Des services adaptés à votre contexte.</h2>
          <p>Nous définissons le périmètre utile avant toute mise en place. Les prestations sur mesure restent proposées sur devis.</p>
        </header>
        <div className="service-offer-grid">
          {category.services.map((service) => <ServiceCard key={service.title} service={service} />)}
        </div>
      </section>

      <section className="service-category-proof" aria-labelledby="proof-title">
        <div>
          <h2 id="proof-title">Ce que vous pouvez attendre.</h2>
          <p>Un service utile doit être lisible, suivi et proportionné. C’est le fil conducteur de chaque intervention.</p>
        </div>
        <ServiceFeatureList items={category.highlights} />
      </section>

      <ServiceCTA
        action={category.cta}
        description="Un premier échange suffit pour cadrer le besoin, les priorités et la bonne manière d’avancer."
        title="Parlons de votre environnement."
      />
    </div>
  );
}
