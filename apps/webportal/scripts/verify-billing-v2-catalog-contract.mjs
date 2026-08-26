/**
 * Contrat « Billing V2, autorite commerciale unique ».
 *
 * Ce script verifie trois choses que ni le compilateur ni les tests unitaires
 * ne peuvent voir :
 *
 *  1. la migration 071 TRADUIT reellement les regles de visibilite des
 *     telechargements au lieu de renommer leur type de cible ;
 *  2. elle n'oublie aucune contrainte de cle etrangere pointant vers une table
 *     legacy depuis une table qui, elle, survit ;
 *  3. l'administration du catalogue ne peut ni reecrire un prix en place, ni
 *     enregistrer un couple fournisseur/environnement qui n'existe pas.
 *
 * Les assertions portent sur les sources : elles restent valides sans base.
 * Ce qu'elles NE prouvent PAS : que le DDL s'execute. Cela demande une base
 * MariaDB reelle et reste a valider avant deploiement.
 */
import assert from "node:assert/strict";
import { readdir, readFile } from "node:fs/promises";

const migrationsDir = new URL(
  "../../../apps/api-internal/Migrations/MariaDb/",
  import.meta.url,
);

async function read(path) {
  return readFile(new URL(`../${path}`, import.meta.url), "utf8");
}

async function readMigration(name) {
  return readFile(new URL(name, migrationsDir), "utf8");
}

const migration048 = await readMigration("048_billing_v2_catalog_seed.sql");
const migration070 = await readMigration(
  "070_billing_v2_catalog_administration.sql",
);
const migration071 = await readMigration(
  "071_drop_legacy_commercial_model.sql",
);

// ---------------------------------------------------------------------------
// 1. Regles de visibilite : traduction, pas renommage
//
// La reference legacy et le code Billing V2 sont deux vocabulaires distincts.
// Se contenter de changer `target_type` rendrait les ressources invisibles pour
// leurs ayants droit, sans aucun signal.
// ---------------------------------------------------------------------------

const KNOWN_MAPPINGS = [
  ["STOCK-PERSO-32", "STORAGE-PERSONAL"],
  ["SAVE-PERSO", "BACKUP-PERSONAL"],
  ["SUPERV-SERVICE", "MONITORING-INTERNAL"],
  ["SUPPORT-LV1", "SUPPORT-STANDARD"],
  ["SUPPORT-LV2", "SUPPORT-PLUS"],
  ["USER-ADD", "USER-ADDITIONAL"],
  ["ACCES-VPN", "VPN-ACCESS"],
  ["ACCES-RDS", "RDS-ACCESS"],
];

for (const [legacy, v2] of KNOWN_MAPPINGS) {
  assert.notEqual(
    legacy,
    v2,
    `Le mapping ${legacy} doit rester un vrai changement de vocabulaire.`,
  );
  assert.match(
    migration048,
    new RegExp(`'${legacy}',\\s*'[a-z_]+',\\s*'${v2}'`),
    `La migration 048 doit porter le mapping ${legacy} -> ${v2}. C'est elle `
      + "qui fait autorite pour la traduction faite en 071.",
  );
}

assert.doesNotMatch(
  migration071,
  /UPDATE\s+download_resource_visibility_rules\s+SET\s+target_type/i,
  "071 ne doit PAS se contenter de renommer `target_type` : la valeur ciblee "
    + "change aussi.",
);

assert.match(
  migration071,
  /JOIN\s+billing_v2_legacy_service_mappings/,
  "071 doit lire `billing_v2_legacy_service_mappings` avant de la supprimer.",
);
assert.match(
  migration071,
  /JOIN\s+billing_v2_legacy_offer_mappings/,
  "071 doit traduire aussi les offres legacy qui correspondent a un preset V2.",
);

for (const [legacy, preset] of [
  ["PACK-BUREAU-12M-COMPT", "pack-bureau-windows-distance"],
  ["PACK-PRO-12M-COMPT", "pack-pro-association"],
]) {
  const offset = migration048.indexOf(`'${legacy}'`);
  assert.ok(offset >= 0, `048 doit contenir le mapping offer ${legacy}.`);
  const block = migration048.slice(offset, offset + 700);
  assert.match(
    block,
    new RegExp(`WHERE p\.code = '${preset}'`),
    `${legacy} doit pointer vers le preset ${preset} dans 048.`,
  );
}

assert.match(
  migration071,
  /LEFT JOIN\s+download_resources[\s\S]{0,180}?WHERE resource\.id IS NULL/,
  "071 doit nettoyer explicitement les regles dont la ressource parente n'existe plus.",
);
assert.doesNotMatch(
  migration071,
  /INSERT IGNORE INTO\s+download_resource_visibility_rules/i,
  "071 ne doit pas masquer une erreur FK avec INSERT IGNORE pendant la traduction.",
);
assert.match(
  migration071,
  /ON DUPLICATE KEY UPDATE id = download_resource_visibility_rules\.id/,
  "071 doit dedoublonner explicitement sans avaler les erreurs de donnees.",
);

for (const kind of ["storage_increment", "legacy_one_time_entitlement"]) {
  assert.match(
    migration071,
    new RegExp(`'${kind}'`),
    `071 doit traiter explicitement le mapping_kind ${kind} plutot que de le `
      + "convertir au mieux.",
  );
}

// Les deux refus et le controle d'orphelins.
for (const guard of [
  /offer_external_reference n''ont aucun equivalent Billing V2/,
  /public_pack_code ne designent aucun billing_v2_offer_presets\.code/,
  /designent un service ou une formule/,
]) {
  assert.match(
    migration071,
    guard,
    "071 doit refuser explicitement plutot que laisser une regle orpheline.",
  );
}

const signalStatements = migration071
  .split(/\r?\n/)
  .filter((line) => !line.trimStart().startsWith("--"))
  .filter((line) => line.includes("SIGNAL SQLSTATE '45000'"));
assert.equal(
  signalStatements.length,
  3,
  "Trois refus explicites sont attendus : references sans equivalent, "
    + "formules inconnues, orphelins residuels.",
);

// En MariaDB une instruction DDL provoque un commit implicite : tout ce qui
// peut faire refuser la migration doit s'executer avant la premiere.
const firstDdl = migration071.search(/^(ALTER TABLE|DROP TABLE)/m);
const lastGuard = migration071.lastIndexOf("SIGNAL SQLSTATE '45000'");
assert.ok(firstDdl > 0, "071 doit contenir du DDL.");
assert.ok(
  lastGuard < firstDdl,
  "Tous les refus de 071 doivent preceder le premier DDL : passe la premiere "
    + "ALTER TABLE, le commit implicite rend la migration non annulable.",
);

// ---------------------------------------------------------------------------
// 2. Aucune cle etrangere legacy oubliee
//
// Une table supprimee entraine ses propres FK. Le piege est la FK detenue par
// une table qui SURVIT : `DROP TABLE` echoue alors en pleine migration, apres
// des commits implicites deja passes.
// ---------------------------------------------------------------------------

const DROPPED_TABLES = [
  "commercial_offers",
  "subscriptions",
  "cart_items",
  "recurring_checkout_items",
  "paypal_webhook_events",
  "stripe_webhook_events",
  "billing_v2_legacy_offer_mappings",
  "billing_v2_legacy_service_mappings",
  "billing_v2_shadow_price_checks",
  "subscription_billing_price_locks",
  "subscription_billing_price_lock_review_required",
  "commercial_document_line_subscriptions",
];

const PRESERVED_TABLES = [
  "commercial_documents",
  "commercial_document_lines",
  "billing_v2_subscription_documents",
  "billing_v2_subscription_price_locks",
  "ad_actions",
];

for (const table of DROPPED_TABLES) {
  assert.match(
    migration071,
    new RegExp(`DROP TABLE IF EXISTS ${table};`),
    `071 doit supprimer la table legacy ${table}.`,
  );
}

for (const table of PRESERVED_TABLES) {
  assert.doesNotMatch(
    migration071,
    new RegExp(`DROP TABLE IF EXISTS ${table};`),
    `${table} doit survivre a 071 : ce n'est pas du catalogue legacy.`,
  );
}

// `billing_v2_subscription_price_locks` et `subscription_billing_price_locks`
// se ressemblent au point d'etre confondus. Le premier est le verrou V2 lu par
// la projection ; le second est son ancetre legacy.
assert.notEqual(
  DROPPED_TABLES.includes("billing_v2_subscription_price_locks"),
  true,
  "Le verrou tarifaire Billing V2 ne doit jamais figurer parmi les suppressions.",
);

/**
 * Contraintes de cle etrangere pointant vers une table legacy, avec la table
 * qui les detient. Balaye toutes les migrations sauf 071 elle-meme.
 */
async function collectLegacyForeignKeys() {
  const files = (await readdir(migrationsDir))
    .filter((name) => /^[0-9]{3}_.*\.sql$/.test(name))
    .filter((name) => !name.startsWith("071_"))
    .sort();

  const found = [];
  for (const file of files) {
    const sql = await readMigration(file);
    let owner = null;
    let constraint = null;
    for (const line of sql.split(/\r?\n/)) {
      const table = line.match(
        /^\s*(?:CREATE TABLE(?: IF NOT EXISTS)?|ALTER TABLE)\s+`?([a-z0-9_]+)`?/i,
      );
      if (table) {
        owner = table[1];
      }

      const named = line.match(/CONSTRAINT\s+`?([a-z0-9_]+)`?/i);
      if (named) {
        constraint = named[1];
      }

      const references = line.match(/REFERENCES\s*`?([a-z0-9_]+)`?\s*\(/i);
      if (references && DROPPED_TABLES.includes(references[1])) {
        found.push({
          file,
          owner,
          constraint,
          target: references[1],
        });
      }
    }
  }

  return found;
}

const legacyForeignKeys = await collectLegacyForeignKeys();
assert.ok(
  legacyForeignKeys.length > 0,
  "Le balayage des migrations doit trouver des FK legacy ; sinon il ne teste "
    + "rien.",
);

for (const fk of legacyForeignKeys) {
  assert.ok(fk.owner, `Table detentrice introuvable dans ${fk.file}.`);
  assert.ok(fk.constraint, `Contrainte anonyme dans ${fk.file} : impossible a `
    + "supprimer par nom.");

  if (DROPPED_TABLES.includes(fk.owner)) {
    // La FK part avec sa table.
    continue;
  }

  assert.ok(
    PRESERVED_TABLES.includes(fk.owner),
    `${fk.owner} detient une FK vers ${fk.target} mais n'est ni supprimee ni `
      + "listee comme conservee : le contrat de 071 est incomplet.",
  );
  assert.match(
    migration071,
    new RegExp(`DROP FOREIGN KEY IF EXISTS ${fk.constraint}`),
    `071 doit supprimer ${fk.constraint} (${fk.owner} -> ${fk.target}) avant `
      + `le DROP TABLE, sinon la migration echoue apres commit implicite.`,
  );
}

// ---------------------------------------------------------------------------
// 3. Prix : versionne et immuable
// ---------------------------------------------------------------------------

const adminServiceCs = await read(
  "../../apps/api-internal/Services/BillingV2CatalogAdministrationService.cs",
);

assert.doesNotMatch(
  adminServiceCs,
  /UPDATE\s+billing_v2_service_prices\s+SET\s+amount_cents/i,
  "Un montant deja publie ne doit jamais etre reecrit en place : les factures "
    + "qu'il a produites s'appuient dessus.",
);
assert.match(
  adminServiceCs,
  /FOR UPDATE/,
  "La revision tarifaire doit verrouiller la fenetre qu'elle remplace.",
);
assert.match(
  adminServiceCs,
  /BeginTransactionAsync/,
  "Fermeture de l'ancienne fenetre et insertion de la nouvelle doivent etre "
    + "atomiques.",
);
assert.match(
  adminServiceCs,
  /BILLING_V2_CATALOG_PRICE_OVERLAP/,
  "Un recouvrement de fenetres doit etre refuse, pas corrige silencieusement.",
);

// Le controle de recouvrement porte sur les cinq dimensions qui identifient
// une fenetre tarifaire. En oublier une laisserait passer deux prix actifs.
for (const dimension of [
  "service_id",
  "tier_id",
  "currency",
  "billing_cadence",
  "charge_trigger",
]) {
  assert.ok(
    adminServiceCs.includes(dimension),
    `Le controle de fenetre tarifaire doit porter sur ${dimension}.`,
  );
  assert.ok(
    migration070.includes(dimension),
    `L'index de recouvrement de 070 doit porter sur ${dimension}.`,
  );
}

// 070 pose un index, pas une contrainte : MariaDB ne sait pas exprimer
// declarativement l'absence de recouvrement. La documentation ne doit pas
// laisser croire l'inverse.
assert.match(
  migration070,
  /MariaDB ne sait pas exprimer/,
  "070 doit dire explicitement que le controle de recouvrement est applicatif.",
);
assert.doesNotMatch(
  migration070,
  /ADD CONSTRAINT[\s\S]{0,80}overlap/i,
  "070 ne doit pas pretendre poser une contrainte SQL de non-recouvrement.",
);

// ---------------------------------------------------------------------------
// 4. Tracabilite des mutations catalogue
// ---------------------------------------------------------------------------

const programCs = await read("../../apps/api-internal/Program.cs");
const catalogRoutes = [
  ...programCs.matchAll(
    /app\.Map(Post|Patch|Put|Delete)\(\s*\n\s*"(\/internal\/admin\/billing-v2\/catalog[^"]*)"([\s\S]*?)\n {4}\}\);/g,
  ),
];

assert.ok(
  catalogRoutes.length >= 13,
  `Les mutations catalogue attendues (service, palier, prix, formule, `
    + `engagement, option de reglement, rattachement fournisseur) representent `
    + `au moins 13 routes ; ${catalogRoutes.length} trouvees.`,
);

for (const [, , route, body] of catalogRoutes) {
  assert.ok(
    body.includes("CatalogMutationResultAsync"),
    `La route ${route} doit passer par CatalogMutationResultAsync : c'est elle `
      + "qui enregistre l'acteur, la cible et le code de refus.",
  );
  assert.ok(
    body.includes("ResolveAdminSessionAsync"),
    `La route ${route} doit exiger une session admin resolue.`,
  );
}

assert.match(
  programCs,
  /ActorUserId: actor\.UserId/,
  "Le journal d'audit doit porter l'acteur reel, pas une constante.",
);

// ---------------------------------------------------------------------------
// 5. Matrice fournisseur / environnement
//
// Stripe n'a pas de « sandbox », PayPal n'a pas de « test ». Valider les deux
// champs separement laisserait enregistrer un rattachement introuvable au
// moment du paiement — panne visible en production seulement.
// ---------------------------------------------------------------------------

const catalogCommandsTs = await read("lib/billing-v2-catalog-commands.ts");
const catalogAdminTsx = await read("components/admin/catalog/CatalogIntegrations.tsx");

for (const source of [adminServiceCs, catalogCommandsTs, catalogAdminTsx]) {
  assert.match(
    source,
    /stripe(?:"|'|\])?\]?\s*[:=]\s*\[?\s*(?:"|')test(?:"|'),\s*(?:"|')live/,
    "Stripe n'accepte que `test` et `live`.",
  );
  assert.match(
    source,
    /paypal(?:"|'|\])?\]?\s*[:=]\s*\[?\s*(?:"|')sandbox(?:"|'),\s*(?:"|')live/,
    "PayPal n'accepte que `sandbox` et `live`.",
  );
}

assert.doesNotMatch(
  adminServiceCs,
  /AllowedEnvironments\s*=\s*\n?\s*\[/,
  "Une liste d'environnements independante du fournisseur reintroduirait les "
    + "couples impossibles.",
);
assert.match(
  adminServiceCs,
  /RequireProviderEnvironment\(\s*\n?\s*provider/,
  "L'environnement doit etre valide en fonction du fournisseur.",
);
assert.doesNotMatch(
  catalogAdminTsx,
  /<option value="sandbox">Sandbox<\/option>[\s\S]{0,200}<option value="test">/,
  "Le formulaire ne doit pas proposer une liste d'environnements figee.",
);

// ---------------------------------------------------------------------------
// 6. Souscription directe : aucune hypothese cote navigateur
// ---------------------------------------------------------------------------

const directSubscribe = await read("components/BillingV2DirectSubscribe.tsx");

assert.doesNotMatch(
  directSubscribe,
  /commitmentCode:\s*"FLEX"/,
  "L'engagement ne doit pas etre code en dur : un achat ponctuel n'engage a "
    + "rien.",
);
assert.match(
  directSubscribe,
  /hasRecurringComponent\(/,
  "L'engagement doit etre deduit des composantes tarifaires selectionnees.",
);
assert.match(
  directSubscribe,
  /billingCadence === "monthly"[\s\S]{0,120}chargeTrigger === "initial_subscription"/,
  "Une composante `subscription_change` ne doit pas compter comme recurrente "
    + "a la souscription initiale.",
);
assert.doesNotMatch(
  directSubscribe,
  /service\.billingType/,
  "`billingType` est une metadonnee d'affichage : elle ne decide d'aucune "
    + "ligne tarifaire.",
);
assert.match(
  directSubscribe,
  /key=\{`\$\{line\.serviceCode\}\|\$\{line\.tierCode \?\? "-"\}\|\$\{line\.billingCadence\}`\}/,
  "Un meme service/palier produit plusieurs lignes : la cadence fait partie de "
    + "l'identite de la ligne.",
);
assert.match(
  directSubscribe,
  /service\.publicVisible && service\.selfServiceOrderable/,
  "Visibilite publique et commandabilite en libre-service restent deux "
    + "drapeaux distincts.",
);
assert.match(
  directSubscribe,
  /priceComponentsFor\(service, null\)\.length > 0/,
  "La presence d'un prix se lit sur les composantes, pas sur un montant "
    + "mensuel positif.",
);

// Le navigateur n'envoie jamais de montant.
for (const forbidden of [
  /amountCents\s*:/,
  /unitAmountCents\s*:/,
  /totalDueNowCents\s*:/,
]) {
  assert.doesNotMatch(
    directSubscribe,
    forbidden,
    "La selection envoyee ne doit porter aucun montant.",
  );
}

// ---------------------------------------------------------------------------
// 7. Rien du modele supprime ne doit survivre dans le SQL d'execution
//
// Un build vert ne prouve rien ici : le SQL est une chaine de caracteres. Une
// colonne supprimee par 071 laissee dans un INSERT ne casse la compilation
// nulle part — elle casse la PRODUCTION, au premier appel, apres migration.
// C'est exactement ce qui s'etait produit avec `commercial_documents.
// subscription_id` et `commercial_document_lines.offer_id`.
// ---------------------------------------------------------------------------

const runtimeSources = await collectRuntimeSql();

// Tables supprimees par 071. Une requete d'execution qui les cible echouera.
const DROPPED_RUNTIME_TABLES = [
  "commercial_offers",
  "subscriptions",
  "cart_items",
  "recurring_checkout_items",
  "paypal_webhook_events",
  "stripe_webhook_events",
  "billing_v2_legacy_offer_mappings",
  "billing_v2_legacy_service_mappings",
  "billing_v2_shadow_price_checks",
];

for (const table of DROPPED_RUNTIME_TABLES) {
  // `` ne suffit pas : `billing_v2_subscriptions` contient `subscriptions`.
  // On exige donc un separateur SQL reel avant le nom.
  const pattern = new RegExp(
    String.raw`(FROM|JOIN|INTO|UPDATE|DELETE\s+FROM)\s+\`?${table}\`?(\s|;|\.|,|$)`,
    "i",
  );
  for (const [file, sql] of runtimeSources) {
    assert.doesNotMatch(
      sql,
      pattern,
      `${file} interroge « ${table} », supprimee par la migration 071.`,
    );
  }
}

// Colonnes supprimees par 071. Le nom seul ne suffit pas a conclure — plusieurs
// tables Billing V2 portent legitimement `subscription_id` — donc on cible
// l'usage reel : la colonne citee dans un INSERT/SELECT/UPDATE de la table qui
// la perd.
const DROPPED_COLUMN_USES = [
  {
    label: "commercial_documents.subscription_id",
    pattern: /INSERT INTO commercial_documents[\s\S]{0,700}?subscription_id/i,
  },
  {
    label: "commercial_document_lines.offer_id",
    pattern: /INSERT INTO commercial_document_lines[\s\S]{0,500}?offer_id/i,
  },
  {
    label: "billing_v2_subscription_price_locks.source_legacy_offer_id",
    pattern: /source_legacy_offer_id/i,
  },
  {
    label: "billing_v2_authoritative_checkout_requests.legacy_offer_id",
    pattern: /legacy_offer_id/i,
  },
  {
    label: "signup_pending.pack_selection_snapshot_json",
    pattern: /pack_selection_snapshot_json/i,
  },
  { label: "last_shadow_status", pattern: /last_shadow_status/i },
  {
    label: "last_shadow_matches_legacy",
    pattern: /last_shadow_matches_legacy/i,
  },
];

for (const { label, pattern } of DROPPED_COLUMN_USES) {
  for (const [file, sql] of runtimeSources) {
    assert.doesNotMatch(
      sql,
      pattern,
      `${file} utilise « ${label} », supprimee par la migration 071.`,
    );
  }
}

// Le scan doit voir quelque chose, sinon il passe pour de mauvaises raisons.
assert.ok(
  runtimeSources.length > 40,
  `Le scan runtime n a lu que ${runtimeSources.length} fichier(s) :`
    + " le garde serait vide de sens.",
);
assert.ok(
  runtimeSources.some(([, sql]) => sql.includes("INSERT INTO commercial_documents")),
  "Le scan doit couvrir l emetteur de documents, ou le defaut avait ete"
    + " introduit.",
);

/**
 * Lit tout le C# d'execution : services, repositories et Program.cs.
 *
 * Les migrations sont exclues — elles ont le droit, et le devoir, de nommer
 * ce qu'elles suppriment.
 */
async function collectRuntimeSql() {
  const roots = [
    "../../apps/api-internal/Services",
    "../../apps/api-internal/Data/Repositories",
  ];
  const files = [["Program.cs", await read("../../apps/api-internal/Program.cs")]];
  for (const root of roots) {
    for (const name of await listCsFiles(root)) {
      files.push([name, await read(name)]);
    }
  }
  return files;
}

async function listCsFiles(relativeRoot) {
  const found = [];
  const entries = await readdir(new URL(`../${relativeRoot}/`, import.meta.url), {
    withFileTypes: true,
  });
  for (const entry of entries) {
    if (entry.isDirectory()) {
      found.push(...(await listCsFiles(`${relativeRoot}/${entry.name}`)));
    } else if (entry.name.endsWith(".cs")) {
      found.push(`${relativeRoot}/${entry.name}`);
    }
  }
  return found;
}

console.log(
  "Contrat Billing V2 catalogue : traduction 071, FK legacy, immuabilite des "
    + "prix, audit, matrice fournisseur, souscription directe et absence de "
    + "SQL runtime sur le modele supprime verifies.",
);
