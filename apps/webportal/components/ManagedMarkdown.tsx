"use client";

import type { ComponentPropsWithoutRef } from "react";
import ReactMarkdown from "react-markdown";

type ManagedMarkdownProps = {
  markdown: string;
  className?: string;
};

export function ManagedMarkdown({
  markdown,
  className = "",
}: ManagedMarkdownProps) {
  return (
    <div className={`managed-markdown ${className}`.trim()}>
      <ReactMarkdown
        components={{
          // Decalage des niveaux de titre au RENDU, pas dans la source.
          //
          // Les pages qui affichent ce composant emettent deja leur propre
          // `<h1>` (titre du contenu administrable, titre de la fiche de
          // pack). Le markdown administrable, lui, part de `#` : `/cgv`
          // sortait 4 `<h1>` et `/politique-confidentialite` 2.
          //
          // Corriger le markdown stocke en base serait annule a la
          // prochaine edition depuis le back-office. Le decalage doit donc
          // vivre ici, ou il s'applique quel que soit le contenu saisi.
          //
          // `h6` n'est pas remappe : il ne peut pas descendre plus bas.
          h1: "h2",
          h2: "h3",
          h3: "h4",
          h4: "h5",
          h5: "h6",
          a: ({ href, ...props }: ComponentPropsWithoutRef<"a">) => (
            <a
              {...props}
              href={href}
              rel={href?.startsWith("http") ? "noreferrer noopener" : undefined}
              target={href?.startsWith("http") ? "_blank" : undefined}
            />
          ),
        }}
      >
        {markdown}
      </ReactMarkdown>
    </div>
  );
}
