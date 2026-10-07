export const FORMULE_HELP_CONTENT = {
  vpn: {
    title: "Accès sécurisé à distance",
    description:
      "Une connexion protégée pour retrouver vos services lorsque vous n’êtes pas sur place.",
  },
  remoteDesktop: {
    title: "Bureau Windows à distance",
    description:
      "Un poste de travail Windows accessible à distance, comme si vous étiez devant l’ordinateur, depuis chez vous ou en déplacement.",
  },
  personalStorage: {
    title: "Espace personnel de fichiers",
    description:
      "Un espace privé pour conserver et retrouver vos documents et fichiers de travail.",
  },
  sharedStorage: {
    title: "Espace partagé",
    description:
      "Espace commun accessible à plusieurs personnes de votre structure pour centraliser les documents d’équipe.",
  },
  personalBackup: {
    title: "Copie de sécurité de vos fichiers",
    description:
      "Une copie de vos fichiers personnels pour préparer leur récupération après une erreur, une suppression ou un incident.",
  },
  sharedBackup: {
    title: "Copie de sécurité de l’espace partagé",
    description:
      "Une copie des fichiers communs pour préparer leur récupération après une erreur, une suppression ou un incident.",
  },
  additionalUser: {
    title: "Accès pour une personne supplémentaire",
    description:
      "Chaque personne ajoutée reçoit son propre accès. L’espace de fichiers, sa copie de sécurité et l’accès à distance de la personne principale ne sont pas ajoutés automatiquement pour elle.",
  },
  supportPlus: {
    title: "Assistance renforcée",
    description:
      "Niveau d’accompagnement renforcé pour les besoins nécessitant davantage d’assistance.",
  },
} as const;
export type FormuleHelpKey = keyof typeof FORMULE_HELP_CONTENT;
