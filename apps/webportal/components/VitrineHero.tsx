import Image from "next/image";
import Link from "next/link";

type HeroAction = { href: string; label: string };

const serviceRail = ["Maintenance", "Réseau", "Sauvegarde", "Accès à distance"];
const rotatingPhrases = [
  "suit votre activité.",
  "sécurise vos accès.",
  "relie vos équipes.",
] as const;

function HeroTitle({ title }: { title: string }) {
  if (title === "Votre informatique suit votre activité.") {
    return <span aria-hidden="true" className="hero-kinetic-visible">
      <span className="hero-kinetic-fixed">Votre informatique</span>
      <span className="hero-kinetic-window">
        <span className="hero-kinetic-track">
          {[...rotatingPhrases, rotatingPhrases[0]].map((phrase, index) =>
            <span className="hero-kinetic-phrase" key={`${phrase}-${index}`}>{phrase}</span>)}
        </span>
      </span>
    </span>;
  }

  return <>{title}</>;
}

export function VitrineHero({
  title,
  body,
  primary,
  secondary,
  mediaId,
}: {
  title: string;
  body: string;
  primary?: HeroAction;
  secondary?: HeroAction;
  mediaId?: string | null;
}) {
  return <section className="vitrine-hero-band">
    <div className="vitrine-hero vitrine-hero-2026">
      <div className="vitrine-hero-copy">
        <p className="vitrine-hero-overline">Zachary IT · Informatique professionnelle</p>
        <h1 aria-label={title === "Votre informatique suit votre activité." ? title : undefined}>
          <HeroTitle title={title} />
        </h1>
        <p className="vitrine-hero-lead">{body}</p>
        <div className="vitrine-hero-actions">
          {primary ? <Link className="button" href={primary.href}>{primary.label}<span aria-hidden="true">↗</span></Link> : null}
          {secondary ? <Link className="button button-secondary" href={secondary.href}>{secondary.label}<span aria-hidden="true">↗</span></Link> : null}
        </div>
      </div>
      <div aria-hidden="true" className="vitrine-hero-media">
        <Image alt="" fetchPriority="high" height={853} loading="eager"
          sizes="(max-width: 820px) 100vw, 50vw"
          src={mediaId ? `/api/site-media/${encodeURIComponent(mediaId)}` : "/brand/network-illustration-motion.webp"}
          unoptimized width={1280} />
        <span className="vitrine-signal vitrine-signal-one" />
        <span className="vitrine-signal vitrine-signal-two" />
        <span className="vitrine-signal vitrine-signal-three" />
      </div>
    </div>
    <div className="vitrine-service-rail">
      <div className="vitrine-service-rail-track">
        {serviceRail.map((service) => <span key={service}>{service}</span>)}
        {serviceRail.map((service) => <span aria-hidden="true" key={`repeat-${service}`}>{service}</span>)}
      </div>
    </div>
  </section>;
}
