import type { SitePageBlock, SitePageBlockItem, SitePageLayout } from "@kermaria/shared";

/**
 * Les mises en page publiées avant la refonte restent en base. Cette projection
 * ne touche qu'aux libellés exacts du modèle initial ; un texte modifié dans
 * l'administration garde toujours la priorité. Aucune donnée commerciale ou
 * aucun prix n'est calculé ici.
 */
const legacyText = new Map<string, string>([
  ["Une informatique fiable, simplement.", "L'informatique de votre activité, suivie de près."],
  ["Dépannage, installation, protection de vos fichiers et conseils : Zachary IT vous accompagne à la maison comme au travail.", "Maintenance, réseau, sauvegarde et accès à distance pour les professionnels, indépendants et petites structures."],
  ["Trouver une solution", "Demander un devis"],
  ["Demander un conseil", "Voir les services"],
  ["Quelle est votre situation ?", "Une aide adaptée à votre organisation"],
  ["Choisissez le point de départ qui vous ressemble.", "La première étape dépend de vos usages, pas d'une formule imposée."],
  ["Particuliers", "PME et commerces"],
  ["Pour retrouver un ordinateur agréable à utiliser, un Wi-Fi fiable et des fichiers importants protégés.", "Pour limiter les interruptions sur les postes, le réseau et les outils utilisés chaque jour."],
  ["Associations", "Indépendants"],
  ["Pour partager les informations plus facilement et continuer à fonctionner quand un bénévole ou un outil manque.", "Pour garder l'accès à vos dossiers et à vos échanges, au bureau comme en déplacement."],
  ["Indépendants et petites entreprises", "Associations et petites structures"],
  ["Pour travailler sereinement avec des outils suivis, des données protégées et un interlocuteur disponible.", "Pour clarifier les accès, la sauvegarde des documents et la continuité des outils partagés."],
  ["Trois étapes pour définir et mettre en place votre solution.", "Une intervention qui commence par le bon diagnostic"],
  ["Vous nous expliquez votre besoin", "Décrivez la situation"],
  ["Une question, une panne ou un projet : décrivez simplement votre situation, même si vous ne savez pas quelle solution choisir.", "Dites-nous ce qui bloque, ce qui doit changer et les outils concernés. Il n'est pas nécessaire de choisir une prestation à l'avance."],
  ["Nous proposons une réponse claire", "Nous cadrons l'intervention"],
  ["Vous savez ce qui est prévu, ce que cela coûte et ce qui se passe ensuite avant de prendre une décision.", "Nous précisons le périmètre utile, les dépendances éventuelles et les conditions avant de proposer une intervention ou un devis."],
  ["Nous restons à vos côtés", "Vous gardez une vue claire"],
  ["Une fois la solution mise en place, nous vérifions qu'elle vous convient et restons disponibles si votre besoin évolue.", "L'intervention, les accès et le suivi prévus sont expliqués pour que vous sachiez ce qui relève de Zachary IT et de vos autres prestataires."],
  ["De quoi avez-vous besoin ?", "Des services pour les problèmes du quotidien"],
  ["Partez de votre problème ou de votre projet. Nous vous aiderons à trouver l'offre adaptée, sans avoir à connaître les termes techniques.", "Chaque prestation part d'une situation concrète. Consultez son périmètre, puis contactez-nous si plusieurs sujets se recoupent."],
  ["Un réseau qui fonctionne", "Assistance et maintenance informatique"],
  ["Retrouvez une connexion stable à la maison ou au travail, là où vous en avez besoin.", "Un poste ralentit, un logiciel bloque, les demandes s'accumulent : faites le point sur les incidents et l'entretien de vos outils."],
  ["Des outils prêts à l'emploi", "Réseau et Wi-Fi professionnel"],
  ["Installation, aide à la prise en main et accès à vos outils, y compris à distance lorsque c'est utile.", "Des coupures, des zones mal couvertes ou des accès difficiles à gérer : reprenez la maîtrise de votre réseau."],
  ["Vos fichiers protégés", "Sauvegarde et continuité"],
  ["Gardez une copie de vos documents importants et préparez leur récupération en cas de problème.", "Identifiez les données à protéger, la fréquence des copies et la façon de les récupérer en cas d'incident."],
  ["Votre activité en ligne", "Accès à distance et serveurs"],
  ["Site, adresse e-mail et services en ligne : nous vous aidons à les mettre en place et à les suivre.", "Organisez l'accès aux outils hors de vos locaux et l'administration des services qui font tourner votre activité."],
  ["Choisissez votre prochaine étape", "Quel est votre prochain pas ?"],
  ["Comparez les solutions proposées ou posez votre question directement.", "Les offres publiées et leurs prix sont détaillés séparément. Pour un besoin particulier, demandez un devis."],
  ["Je veux comparer les offres", "Comparer les offres"],
  ["Découvrez à quoi elles servent et ce qu'elles comprennent.", "Consultez les configurations publiées et ce qu'elles comprennent."],
  ["Je veux connaître les tarifs", "Consulter les tarifs"],
  ["Consultez les prix affichés et les prestations proposées sur devis.", "Retrouvez les prix affichés et les prestations étudiées sur devis."],
  ["Je ne sais pas encore", "Faire le point"],
  ["Quelques questions simples vous aideront à situer votre besoin.", "Un questionnaire peut vous aider à décrire vos priorités."],
  ["M'orienter", "Ouvrir le diagnostic"],
  ["Un projet ou une question ? Parlons-en.", "Parlons de ce qui doit fonctionner chez vous."],
  ["Expliquez votre besoin en quelques mots. Nous vous répondrons avec une prochaine étape claire, sans vous demander de choisir seul une solution.", "Expliquez le contexte et les contraintes de votre activité. Nous préciserons ensemble la suite utile avant tout devis."],
  ["Parler de mon besoin", "Demander un devis"],
  ["Une aide claire pour votre informatique, à la maison comme au travail.", "Maintenance, réseau et sauvegarde pour les professionnels et les petites structures."],
  ["Parlons de votre besoin", "Parlons de votre besoin"],
  ["Décrivez votre situation en quelques lignes : un problème à résoudre, un projet à lancer ou une question à poser. Nous vous aiderons à trouver la prochaine étape.", "Décrivez la panne, le projet ou la question qui vous amène. Nous vous répondrons pour préciser le périmètre et la suite à donner."],
  ["Ce qui se passe ensuite", "Après votre message"],
  ["Vous envoyez votre message", "Nous lisons votre demande"],
  ["Nous recevons votre demande par e-mail.", "Votre message nous donne le contexte et les coordonnées pour vous répondre."],
  ["Nous vous répondons", "Nous précisons le besoin"],
  ["Nous utilisons l'adresse indiquée pour vous répondre et préciser votre besoin.", "Nous revenons vers vous si des informations manquent pour comprendre la situation."],
  ["Nous convenons de la suite", "Nous définissons la suite"],
  ["Si votre demande nécessite une étude, nous vous expliquons les points à vérifier avant de proposer un devis.", "Si une étude est nécessaire, nous précisons ce qu'il faut vérifier avant de proposer un devis."],
]);

const legacyHomeHrefs: Record<string, string> = {
  "Trouver une solution": "/contact",
  "Demander un conseil": "/services",
  "Particuliers": "/services/support-it",
  "Associations": "/services/sauvegarde-externalisee",
  "Indépendants et petites entreprises": "/services",
  "Un réseau qui fonctionne": "/services/support-it",
  "Des outils prêts à l'emploi": "/services/reseau-securite",
  "Vos fichiers protégés": "/services/sauvegarde-externalisee",
  "Votre activité en ligne": "/services/vpn-entreprise",
  "Parler de mon besoin": "/contact",
};

function copy(value: string | null): string | null {
  return value === null ? null : legacyText.get(value) ?? value;
}

function itemWithCopy(item: SitePageBlockItem, pageKey: string): SitePageBlockItem {
  return {
    ...item,
    title: copy(item.title) ?? item.title,
    body: copy(item.body),
    label: copy(item.label),
    href: pageKey === "/" && legacyHomeHrefs[item.title]
      ? legacyHomeHrefs[item.title] : item.href,
  };
}

function blockWithCopy(block: SitePageBlock, pageKey: string): SitePageBlock {
  if (pageKey !== "/" && pageKey !== "/footer" && pageKey !== "/contact") return block;
  return {
    ...block,
    title: copy(block.title),
    body: copy(block.body),
    label: copy(block.label),
    href: pageKey === "/" && block.type === "hero" && block.label === "Trouver une solution"
      ? "/contact" : block.href,
    items: block.items
      ?.filter((item) => !(pageKey === "/" && block.type === "services"
        && item.title === "Une aide quand il faut"
        && item.body === "Obtenez une réponse quand un outil bloque, et un suivi pour éviter que les problèmes s'accumulent."))
      .map((item) => itemWithCopy(item, pageKey)) ?? null,
  };
}

export function presentPublicSiteLayout(layout: SitePageLayout): SitePageLayout {
  if (layout.area !== "public") return layout;
  if (!["/", "/footer", "/contact"].includes(layout.pageKey)) return layout;
  return { ...layout, blocks: layout.blocks.map((block) => blockWithCopy(block, layout.pageKey)) };
}
