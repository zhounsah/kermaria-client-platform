import Link from "next/link";

/**
 * Federation vers les modules deja specialises (specification, section 18).
 *
 * Le Centre de configuration ne recree pas ces surfaces : chacune est deja
 * l'autorite sur son domaine, et une seconde surface d'edition finirait par
 * diverger de la premiere. Ce qui manquait n'etait pas un editeur de plus, mais
 * la reponse a « ou se change reellement cette chose-la ».
 *
 * Chaque entree dit donc ce que le module detient et ce que le Centre laisse
 * volontairement hors de sa portee.
 */
type FederatedModule = {
  href: string;
  label: string;
  authority: string;
  boundary: string;
};

const modules: FederatedModule[] = [
  {
    href: "/admin/content",
    label: "Contenus administrables",
    authority:
      "Textes du portail client rattachés à une clé de contenu, modifiables sans livraison.",
    boundary:
      "Le Centre administre les modèles de messages sortants, pas les textes affichés dans le portail.",
  },
  {
    href: "/admin/editorial",
    label: "Éditorial et pages publiques",
    authority:
      "Pages de la vitrine, FAQ et ressources publiées, avec leur cycle brouillon/publié.",
    boundary:
      "Le Centre ne contient aucun éditeur de CMS : un second éditeur divergerait du premier.",
  },
  {
    href: "/admin/catalog",
    label: "Catalogue commercial et Billing V2",
    authority:
      "Offres, prix et cycles de facturation. Billing V2 reste l'autorité commerciale et fiscale.",
    boundary:
      "Le Centre n'affiche l'état de Billing V2 qu'en lecture, et n'ouvre aucun calcul de prix ni de taxe.",
  },
  {
    href: "/admin/public-pack-catalog",
    label: "Vitrine des formules",
    authority:
      "Présentation publique des formules et de leur mise en avant.",
    boundary:
      "Certains libellés commerciaux restent codés dans le portail ; ils migreront vers cette source, pas vers le Centre.",
  },
  {
    href: "/admin/downloads",
    label: "Téléchargements",
    authority:
      "Ressources téléchargeables, leurs catégories et les fichiers eux-mêmes.",
    boundary:
      "Le Centre montre la racine de stockage et son état d'accès, sans toucher aux fichiers.",
  },
  {
    href: "/admin/backups",
    label: "Sauvegardes Veeam",
    authority:
      "Rapports de sauvegarde poussés par le collecteur et leur rattachement client.",
    boundary:
      "Le collecteur est externe et pousse ses rapports : rien ne se déclenche depuis le Centre.",
  },
  {
    href: "/admin/koxo",
    label: "KoXo et annuaire",
    authority:
      "Synchronisation des identités et rattachement des comptes Active Directory.",
    boundary:
      "Le mode annuaire et les racines autorisées sont montrés en lecture dans la vue runtime : les rendre modifiables depuis une page web élargirait la portée d'écriture sur l'annuaire.",
  },
  {
    href: "/admin/email-log",
    label: "Journal d'envoi e-mail",
    authority:
      "Trace des envois réels, de leurs destinataires et de leurs échecs.",
    boundary:
      "Le Centre en tire l'état SMTP et le dernier échec ; le détail par message se lit ici.",
  },
  {
    href: "/admin/demo",
    label: "Comptes de démonstration",
    authority:
      "Comptes de démonstration créés, leur expiration et leur conversion.",
    boundary:
      "Le Centre administre les modèles de contenu semés sur ces comptes, pas les comptes eux-mêmes.",
  },
  {
    href: "/admin/audit-logs",
    label: "Journal d'audit général",
    authority: "Toute l'activité auditée du portail, sans restriction de domaine.",
    boundary:
      "L'audit du Centre lit ce même journal, restreint aux actions de configuration.",
  },
];

export function AdminSettingsFederation() {
  return (
    <section
      aria-label="Modules spécialisés"
      className="content-panel section-card admin-settings-federation"
    >
      <span className="card-kicker">Navigation</span>
      <h2>Ce qui se change ailleurs</h2>
      <p className="muted">
        Ces modules sont déjà l&apos;autorité sur leur domaine. Le Centre ne les
        recrée pas : une seconde surface d&apos;édition finirait par diverger de
        la première. Chaque entrée indique ce qu&apos;elle détient et ce que le
        Centre laisse volontairement hors de sa portée.
      </p>
      <ul>
        {modules.map((module) => (
          <li key={module.href}>
            <Link href={module.href}>{module.label}</Link>
            <p>{module.authority}</p>
            <p className="muted">{module.boundary}</p>
          </li>
        ))}
      </ul>
    </section>
  );
}
