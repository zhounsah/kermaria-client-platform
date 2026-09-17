import assert from "node:assert/strict";

import {
  resolvePublicCommercialOrdering,
} from "../lib/public-commercial-ordering.ts";
import {
  resolveStorefrontDirectTariffAction,
} from "../lib/storefront-content.ts";

const quote = resolvePublicCommercialOrdering({
  requestedMode: "quote",
  legacyMode: "offer_component",
  offerCount: 1,
  preferredOfferCta: { label: "Voir l'offre", href: "/formules/pack-acces-distance" },
  directCta: null,
});
assert.deepEqual(quote, {
  orderingMode: "quote",
  offerCount: 0,
  primaryCta: { label: "Demander un devis", href: "/contact" },
}, "un prix public peut rester affiché alors que le parcours imposé est le devis");

const explicitOffer = resolvePublicCommercialOrdering({
  requestedMode: "offer_component",
  legacyMode: "quote",
  offerCount: 1,
  preferredOfferCta: { label: "Voir l'offre", href: "/formules/pack-acces-distance" },
  directCta: null,
});
assert.equal(explicitOffer.orderingMode, "offer_component");
assert.deepEqual(explicitOffer.primaryCta, {
  label: "Voir l'offre",
  href: "/formules/pack-acces-distance",
}, "une destination d'offre explicitement configurée reste utilisée");

const multipleOffers = resolvePublicCommercialOrdering({
  requestedMode: "offer_component",
  legacyMode: "quote",
  offerCount: 2,
  preferredOfferCta: null,
  directCta: null,
});
assert.deepEqual(multipleOffers.primaryCta, {
  label: "Comparer les offres",
  href: "/offres",
}, "plusieurs offres sans préférence ne sélectionnent jamais arbitrairement un preset");

const invalidDirect = resolvePublicCommercialOrdering({
  requestedMode: "direct",
  legacyMode: "quote",
  offerCount: 0,
  preferredOfferCta: null,
  directCta: null,
});
assert.equal(invalidDirect.orderingMode, "quote");
assert.equal(invalidDirect.primaryCta.href, "/contact");

const direct = resolvePublicCommercialOrdering({
  requestedMode: "direct",
  legacyMode: "quote",
  offerCount: 0,
  preferredOfferCta: null,
  directCta: {
    label: "Configurer",
    href: "/services/vps/choisir?serviceCode=VPS-LOCAL&tierCode=NANO",
  },
});
assert.deepEqual(direct, {
  orderingMode: "direct",
  offerCount: 0,
  primaryCta: {
    label: "Configurer",
    href: "/services/vps/choisir?serviceCode=VPS-LOCAL&tierCode=NANO",
  },
}, "le direct ne passe que lorsqu'un configurateur individuel réel est fourni");

assert.equal(
  resolveStorefrontDirectTariffAction("VPS-LOCAL", false, [{
    code: "NANO", monthlyAmountCents: 590, publicSelectable: true,
  }]),
  null,
  "un configurateur existant ne peut pas être exposé sans autorisation libre-service",
);
assert.deepEqual(
  resolveStorefrontDirectTariffAction("VPS-LOCAL", true, [{
    code: "NANO", monthlyAmountCents: 590, publicSelectable: true,
  }]),
  {
    label: "Configurer",
    href: "/services/vps/choisir?serviceCode=VPS-LOCAL&tierCode=NANO",
  },
  "le mode direct conserve la destination individuelle réelle quand elle est autorisée",
);

const legacy = resolvePublicCommercialOrdering({
  requestedMode: null,
  legacyMode: "offer_component",
  offerCount: 1,
  preferredOfferCta: { label: "Voir l'offre", href: "/formules/pack-acces-distance" },
  directCta: {
    label: "Configurer",
    href: "/services/vps/choisir?serviceCode=VPS-LOCAL&tierCode=NANO",
  },
});
assert.equal(legacy.orderingMode, "offer_component", "le repli historique ne publie jamais direct");

console.log("Modèle de commercialisation publique vérifié.");
