export const FORMULE_HELP_CONTENT = {
  vpn: {
    title: "Accès sécurisé à distance (VPN)",
    description:
      "Connexion sécurisée qui permet d’accéder à vos services à distance sans exposer directement votre infrastructure sur Internet.",
  },
  remoteDesktop: {
    title: "Bureau Windows à distance",
    description:
      "Un poste de travail Windows accessible à distance, comme si vous étiez devant l’ordinateur, depuis chez vous ou en déplacement.",
  },
  personalStorage: {
    title: "Stockage personnel",
    description:
      "Espace privé réservé à un utilisateur pour stocker ses documents et fichiers de travail.",
  },
  sharedStorage: {
    title: "Espace partagé",
    description:
      "Espace commun accessible à plusieurs personnes de votre structure pour centraliser les documents d’équipe.",
  },
  personalBackup: {
    title: "Sauvegarde du stockage personnel",
    description:
      "Copie de sécurité de vos fichiers personnels permettant leur restauration en cas d’erreur, de suppression ou d’incident.",
  },
  sharedBackup: {
    title: "Sauvegarde de l’espace partagé",
    description:
      "Copie de sécurité des fichiers de l’espace partagé permettant leur restauration en cas d’erreur, de suppression ou d’incident.",
  },
  additionalUser: {
    title: "Utilisateur supplémentaire",
    description:
      "Ajoute un compte nominatif supplémentaire. Le stockage personnel, sa sauvegarde, l’accès sécurisé et le bureau à distance du titulaire ne sont pas automatiquement dupliqués.",
  },
  supportPlus: {
    title: "Support Plus",
    description:
      "Niveau d’accompagnement renforcé pour les besoins nécessitant davantage d’assistance.",
  },
} as const;
export type FormuleHelpKey = keyof typeof FORMULE_HELP_CONTENT;
