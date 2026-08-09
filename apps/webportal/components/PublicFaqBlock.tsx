import { ManagedMarkdown } from "@/components/ManagedMarkdown";
import { getPublicFaq } from "@/lib/internal-api";

type PublicFaqBlockProps = {
  scope: string;
  title?: string;
};

export async function PublicFaqBlock({
  scope,
  title = "Questions fréquentes",
}: PublicFaqBlockProps) {
  const result = await getPublicFaq(scope);
  if (result.error || result.data.length === 0) {
    return null;
  }

  return (
    <section className="public-faq" aria-labelledby={`faq-${scope}`}>
      <h2 id={`faq-${scope}`}>{title}</h2>
      <div className="public-faq-list">
        {result.data.map((item) => (
          <details className="public-faq-item" key={item.id}>
            <summary>{item.title}</summary>
            <ManagedMarkdown markdown={item.bodyMarkdown} />
          </details>
        ))}
      </div>
    </section>
  );
}
