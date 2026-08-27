import { Fragment } from "react";
import Link from "next/link";
import {
  ArrowRight,
  Check,
  Cloud,
  Headphones,
  Mail,
  ShieldCheck,
  type LucideIcon,
} from "lucide-react";

import type {
  PublicService,
  ServiceCallToAction,
  ServiceCategory,
  ServiceIconKey,
} from "@/lib/public-services";

const SERVICE_ICONS: Record<ServiceIconKey, LucideIcon> = {
  cloud: Cloud,
  mail: Mail,
  shield: ShieldCheck,
  headphones: Headphones,
};

export type ServiceBreadcrumbItem = {
  name: string;
  path: string;
};

export function ServiceBreadcrumb({
  items,
}: {
  items: readonly ServiceBreadcrumbItem[];
}) {
  return (
    <nav aria-label="Fil d’Ariane" className="service-breadcrumb">
      <Link href="/">Accueil</Link>
      {items.map((item, index) => {
        const current = index === items.length - 1;
        return (
          <Fragment key={item.path}>
            <span aria-hidden="true">/</span>
            {current ? (
              <span aria-current="page">{item.name}</span>
            ) : (
              <Link href={item.path}>{item.name}</Link>
            )}
          </Fragment>
        );
      })}
    </nav>
  );
}

export function ServiceHero({
  title,
  description,
  action,
  compact = false,
}: {
  title: string;
  description: string;
  action: ServiceCallToAction;
  compact?: boolean;
}) {
  return (
    <header className={compact ? "service-hero service-hero-compact" : "service-hero"}>
      <div>
        <h1>{title}</h1>
        <p>{description}</p>
      </div>
      <Link className="button" href={action.href}>
        {action.label}
      </Link>
    </header>
  );
}

export function ServiceCategoryCard({ category }: { category: ServiceCategory }) {
  const Icon = SERVICE_ICONS[category.icon];

  return (
    <article className="service-category-card">
      <div className="service-category-icon" aria-hidden="true">
        <Icon size={24} strokeWidth={1.75} />
      </div>
      <h3>{category.shortTitle}</h3>
      <p>{category.description}</p>
      <Link
        aria-label={`Découvrir ${category.shortTitle}`}
        className="service-inline-link"
        href={`/services/${category.slug}`}
      >
        Découvrir <ArrowRight aria-hidden="true" size={17} strokeWidth={1.8} />
      </Link>
    </article>
  );
}

export function ServiceCard({ service }: { service: PublicService }) {
  return (
    <article className="service-offer-card">
      <h3>{service.title}</h3>
      <p>{service.description}</p>
      <ul>
        {service.details.map((detail) => <li key={detail}>{detail}</li>)}
      </ul>
      <Link className="service-inline-link" href={service.cta.href}>
        {service.cta.label} <ArrowRight aria-hidden="true" size={17} strokeWidth={1.8} />
      </Link>
    </article>
  );
}

export function ServiceFeatureList({ items }: { items: string[] }) {
  return (
    <ul className="service-feature-list">
      {items.map((item) => (
        <li key={item}>
          <Check aria-hidden="true" size={18} strokeWidth={2} />
          <span>{item}</span>
        </li>
      ))}
    </ul>
  );
}

export function ServiceCTA({
  title,
  description,
  action,
}: {
  title: string;
  description: string;
  action: ServiceCallToAction;
}) {
  return (
    <section className="service-cta">
      <div>
        <h2>{title}</h2>
        <p>{description}</p>
      </div>
      <Link className="button button-secondary" href={action.href}>
        {action.label}
      </Link>
    </section>
  );
}
