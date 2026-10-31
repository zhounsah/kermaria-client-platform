import type { ComponentPropsWithoutRef } from "react";

const graphemeSegmenter = typeof Intl.Segmenter === "function"
  ? new Intl.Segmenter("fr", { granularity: "grapheme" })
  : null;

/** Le nom du H1 reste entier pour les technologies d'assistance. */
export function AnimatedPublicH1({ text, ...props }: Omit<ComponentPropsWithoutRef<"h1">, "children"> & { text: string }) {
  return <h1 {...props} aria-label={props["aria-label"] ?? text}><LetterWave text={text} /></h1>;
}

/** Visuel uniquement : les mots gardent leurs retours à la ligne habituels. */
export function LetterWave({ text }: { text: string }) {
  let letterIndex = 0;
  return <span aria-hidden="true" className="h1-letter-wave">
    {(text.match(/\S+|\s+/gu) ?? []).map((part, partIndex) => {
      if (/^\s+$/u.test(part)) return part;
      const graphemes = graphemeSegmenter
        ? Array.from(graphemeSegmenter.segment(part), ({ segment }) => segment)
        : Array.from(part);
      return <span className={graphemes.length > 14 ? "h1-letter-word h1-letter-word-long" : "h1-letter-word"} key={partIndex}>
        {graphemes.map((segment, letterInWord) => {
          const delay = letterIndex++ * 18;
          return <span className="h1-letter" key={letterInWord} style={{ animationDelay: `${delay}ms` }}>{segment}</span>;
        })}
      </span>;
    })}
  </span>;
}
