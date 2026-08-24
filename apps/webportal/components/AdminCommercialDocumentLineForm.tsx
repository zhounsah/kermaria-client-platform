"use client";

import type {
  CommercialDocumentLine,
  CommercialDocumentLineMutationResponse,
} from "@kermaria/shared";
import { FormEvent, useRef, useState } from "react";
import { useRouter } from "next/navigation";

import { FormMessage } from "@/components/FormMessage";
import { SubmitButton } from "@/components/SubmitButton";
import { requestBffJson } from "@/lib/client-api";
import type { CatalogLineTemplate } from "@/lib/commercial-document-templates";

/**
 * Ligne d'un document commercial.
 *
 * La ligne est un <b>instantane</b> : libelle, description, unite, prix
 * unitaire et taux de TVA sont recopies dans le document et lui appartiennent
 * ensuite. Le catalogue ne sert qu'a pre-remplir la saisie ; aucun lien n'est
 * conserve. C'est ce qui permet a un devis emis l'an dernier de rester lisible
 * apres une nouvelle version de prix, et c'est pour cela que la colonne
 * `offer_id` a disparu plutot que d'etre repointee vers Billing V2.
 */
type AdminCommercialDocumentLineFormProps = {
  documentId: string;
  templates: readonly CatalogLineTemplate[];
  line?: CommercialDocumentLine;
  disabled?: boolean;
};

export function AdminCommercialDocumentLineForm({
  documentId,
  templates,
  line,
  disabled = false,
}: AdminCommercialDocumentLineFormProps) {
  const router = useRouter();
  const isSubmittingRef = useRef(false);
  const [label, setLabel] = useState(line?.label ?? "");
  const [description, setDescription] = useState(line?.description ?? "");
  const [quantity, setQuantity] = useState(String(line?.quantity ?? 1));
  const [unitLabel, setUnitLabel] = useState(line?.unitLabel ?? "");
  const [unitPriceCents, setUnitPriceCents] = useState(
    String(line?.unitPriceCents ?? 0),
  );
  const [taxRateBasisPoints, setTaxRateBasisPoints] = useState(
    line?.taxRateBasisPoints === null || line?.taxRateBasisPoints === undefined
      ? ""
      : String(line.taxRateBasisPoints),
  );
  const [sortOrder, setSortOrder] = useState(String(line?.sortOrder ?? 0));
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [message, setMessage] = useState<{
    tone: "success" | "error";
    text: string;
  } | null>(null);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (disabled || isSubmittingRef.current) {
      return;
    }

    const parsedQuantity = Number.parseFloat(quantity);
    const parsedPrice = Number.parseInt(unitPriceCents, 10);
    const parsedSort = Number.parseInt(sortOrder, 10);
    const parsedTax = taxRateBasisPoints.trim()
      ? Number.parseInt(taxRateBasisPoints, 10)
      : null;
    const payload = {
      label: label.trim(),
      description: description.trim(),
      quantity: parsedQuantity,
      unitLabel: unitLabel.trim(),
      unitPriceCents: parsedPrice,
      taxRateBasisPoints: parsedTax,
      sortOrder: parsedSort,
    };

    if (
      !Number.isFinite(parsedQuantity)
      || parsedQuantity <= 0
      || !Number.isInteger(parsedPrice)
      || parsedPrice < 0
      || !Number.isInteger(parsedSort)
      || parsedSort < 0
      || payload.label.length < 2
    ) {
      setMessage({
        tone: "error",
        text: "La ligne doit contenir une quantité, un prix et un libellé valides.",
      });
      return;
    }

    isSubmittingRef.current = true;
    setIsSubmitting(true);
    setMessage(null);

    const result = await requestBffJson<CommercialDocumentLineMutationResponse>(
      line
        ? `/api/admin/commercial-documents/${encodeURIComponent(documentId)}/lines/${encodeURIComponent(line.id)}`
        : `/api/admin/commercial-documents/${encodeURIComponent(documentId)}/lines`,
      {
        method: line ? "PATCH" : "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
      },
    );

    if (result.ok) {
      setMessage({
        tone: "success",
        text: line
          ? "La ligne a été mise à jour."
          : "La ligne a été ajoutée.",
      });
      router.refresh();
    } else {
      setMessage({ tone: "error", text: result.error.message });
    }

    isSubmittingRef.current = false;
    setIsSubmitting(false);
  }

  return (
    <form className="form-card compact-form-card" onSubmit={handleSubmit}>
      <div className="form-grid">
        <label>
          Pré-remplir depuis le catalogue
          <select
            disabled={disabled}
            onChange={(event) => {
              const template = templates.find(
                (candidate) => candidate.key === event.target.value,
              );
              if (!template) {
                return;
              }

              // Recopie ponctuelle : la ligne ne garde aucune trace du prix
              // catalogue d'origine, et reste modifiable ensuite.
              setLabel(template.label);
              setDescription(template.description ?? "");
              setUnitLabel(template.unitLabel);
              setUnitPriceCents(String(template.unitPriceCents));
              setTaxRateBasisPoints(
                template.taxRateBasisPoints === null
                  ? ""
                  : String(template.taxRateBasisPoints),
              );
            }}
            value=""
          >
            <option value="">Saisie libre</option>
            {templates.map((template) => (
              <option key={template.key} value={template.key}>
                {template.label}
                {" · "}
                {template.priceCode}
              </option>
            ))}
          </select>
        </label>
        <label>
          Libellé
          <input
            disabled={disabled}
            maxLength={200}
            onChange={(event) => setLabel(event.target.value)}
            value={label}
          />
        </label>
      </div>
      <label>
        Description
        <textarea
          disabled={disabled}
          maxLength={1000}
          onChange={(event) => setDescription(event.target.value)}
          rows={3}
          value={description}
        />
      </label>
      <div className="form-grid">
        <label>
          Quantité
          <input
            disabled={disabled}
            inputMode="decimal"
            onChange={(event) => setQuantity(event.target.value)}
            value={quantity}
          />
        </label>
        <label>
          Unité
          <input
            disabled={disabled}
            maxLength={40}
            onChange={(event) => setUnitLabel(event.target.value)}
            value={unitLabel}
          />
        </label>
      </div>
      <div className="form-grid">
        <label>
          Prix unitaire HT (centimes)
          <input
            disabled={disabled}
            inputMode="numeric"
            onChange={(event) => setUnitPriceCents(event.target.value)}
            value={unitPriceCents}
          />
        </label>
        <label>
          TVA (basis points)
          <input
            disabled={disabled}
            inputMode="numeric"
            onChange={(event) => setTaxRateBasisPoints(event.target.value)}
            value={taxRateBasisPoints}
          />
        </label>
      </div>
      <label>
        Ordre
        <input
          disabled={disabled}
          inputMode="numeric"
          onChange={(event) => setSortOrder(event.target.value)}
          value={sortOrder}
        />
      </label>
      {message ? (
        <FormMessage
          title={message.tone === "success" ? "Ligne enregistrée" : "Échec"}
          tone={message.tone}
        >
          <p>{message.text}</p>
        </FormMessage>
      ) : null}
      <SubmitButton
        disabled={disabled}
        idleLabel={line ? "Mettre à jour la ligne" : "Ajouter la ligne"}
        isSubmitting={isSubmitting}
        submittingLabel="Enregistrement..."
      />
    </form>
  );
}
