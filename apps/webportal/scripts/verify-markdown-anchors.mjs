import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import vm from "node:vm";
import ts from "typescript";

async function loadTypeScriptModule(path, imports = {}) {
  const source = await readFile(new URL(`../${path}`, import.meta.url), "utf8");
  const output = ts.transpileModule(source, {
    compilerOptions: {
      esModuleInterop: true,
      module: ts.ModuleKind.CommonJS,
      target: ts.ScriptTarget.ES2022,
    },
  }).outputText;
  const loadedModule = { exports: {} };
  const context = {
    exports: loadedModule.exports,
    module: loadedModule,
    require(name) {
      if (name in imports) {
        return imports[name];
      }
      throw new Error(`Import inattendu dans le test Markdown anchors: ${name}`);
    },
  };
  vm.runInNewContext(output, context, { filename: path });
  return loadedModule.exports;
}

const anchorsModule = await loadTypeScriptModule("lib/markdown-anchors.ts");
const tocModule = await loadTypeScriptModule("lib/markdown-toc.ts", {
  "@/lib/markdown-anchors": anchorsModule,
});

const markdown = [
  "## Introduction",
  "",
  "Texte.",
  "",
  "## Sauvegarde",
  "",
  "Texte.",
  "",
  "## Introduction",
].join("\n");

const expectedIds = ["introduction", "sauvegarde", "introduction-2"];

for (let index = 0; index < 5; index += 1) {
  assert.equal(
    JSON.stringify(
      anchorsModule.getMarkdownHeadingAnchors(markdown).map((heading) => heading.id),
    ),
    JSON.stringify(expectedIds),
    "Les IDs doivent rester stables sur plusieurs calculs successifs.",
  );
}

const toc = tocModule.extractMarkdownToc(markdown);
const renderedHeadingIds = anchorsModule
  .getMarkdownHeadingAnchors(markdown)
  .filter((heading) => heading.level === 2 || heading.level === 3)
  .map((heading) => heading.id);

assert.equal(
  JSON.stringify(toc.map((heading) => heading.id)),
  JSON.stringify(renderedHeadingIds),
  "Le sommaire et les headings rendus doivent partager les mêmes anchors.",
);

console.log("Vérification des anchors Markdown réussie.");
