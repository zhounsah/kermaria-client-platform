using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Services;

public sealed class MockSitePageStore
{
    public ConcurrentDictionary<string, SitePageLayout> Pages { get; } = new();
    public ConcurrentDictionary<string, List<SitePageRevision>> Revisions { get; } = new();
    public ConcurrentDictionary<string, Dictionary<long, IReadOnlyList<SitePageBlock>>> Snapshots { get; } = new();
    public ConcurrentDictionary<string, (SiteMediaAsset Metadata, byte[] Data)> Media { get; } = new();
    public object Gate { get; } = new();
}

public sealed partial class SitePageService
{
    private const int MaxImageBytes = 5 * 1024 * 1024;
    private const int MaxBlocks = 40;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> BlockTypes = new(StringComparer.Ordinal)
    { "route_content", "text", "image", "cards", "faq", "link", "form", "footer_brand", "footer_links",
      "hero", "audiences", "steps", "services", "offer_path", "final_cta", "offers_story", "diagnostic_intro", "contact_intro", "contact_steps", "data_rights_intro", "widget" };
    private static readonly HashSet<string> FormActions = new(StringComparer.Ordinal)
    { "contact", "data_request" };
    private static readonly HashSet<string> FormFieldTypes = new(StringComparer.Ordinal)
    { "text", "email", "number", "select", "checkbox" };
    private sealed record WidgetPageRule(
        string[] Required, string[] Optional, string[]? Initial = null,
        SitePageBlock[]? InitialBlocks = null);
    private static readonly IReadOnlyDictionary<(string Area, string PageKey), WidgetPageRule>
        WidgetPages = new Dictionary<(string, string), WidgetPageRule>
        {
            [("public", "/offres")] = new(
                ["offers_intro", "offers_configure", "offers_overview",
                 "offers_comparison"],
                ["offers_demo", "offers_help"],
                InitialBlocks:
                [
                    new SitePageBlock("intro", "widget", WidgetKey: "offers_intro"),
                    new SitePageBlock("story", "offers_story",
                        "Un incident matériel ne devrait pas devenir une perte définitive.",
                        "Conservez une copie de vos documents importants dans un lieu séparé. Une copie conservée au même endroit que votre matériel ne couvre pas tous les sinistres ; nous vous aidons à préparer leur récupération.",
                        "/contact", "Demander un accompagnement", Items:
                        [
                            new("Dossier de secours numérique", "Conservez factures, contrats, garanties et photos utiles difficiles à reconstituer après un sinistre.", null),
                            new("Continuer à travailler", "Protégez les documents et fichiers nécessaires pour reprendre plus vite après un problème.", null),
                            new("Un accompagnement local", "À Guichen, Zachary IT explique ce qui est protégé et ce qui reste à prévoir.", null)
                        ]),
                    new SitePageBlock("configure", "widget", WidgetKey: "offers_configure"),
                    new SitePageBlock("demo", "widget", WidgetKey: "offers_demo"),
                    new SitePageBlock("overview", "widget", WidgetKey: "offers_overview"),
                    new SitePageBlock("comparison", "widget", WidgetKey: "offers_comparison")
                ]),
            [("public", "/offres/[slug]")] = new(
                ["offer_sheet_intro", "offer_sheet_back", "offer_sheet_summary",
                 "offer_sheet_services", "offer_sheet_details", "offer_sheet_source"],
                []),
            [("public", "/panier")] = new(
                ["cart_intro", "cart_items", "cart_summary"],
                []),
            [("public", "/souscription")] = new(
                ["checkout_intro", "checkout_details", "checkout_summary"],
                []),
            [("public", "/formules")] = new(
                ["formules_intro", "formules_catalog"],
                ["formules_note", "formules_help"],
                ["formules_intro", "formules_catalog", "formules_note"]),
            [("public", "/formules/[code]")] = new(
                ["formule_detail_breadcrumb", "formule_detail_intro",
                 "formule_detail_configurator"],
                [],
                InitialBlocks:
                [
                    new SitePageBlock("breadcrumb", "widget", WidgetKey: "formule_detail_breadcrumb"),
                    new SitePageBlock("intro", "widget", WidgetKey: "formule_detail_intro"),
                    new SitePageBlock("configurator", "widget", WidgetKey: "formule_detail_configurator"),
                    new SitePageBlock("pricing-help", "text", "Comment lire le prix ?",
                        "Le récapitulatif affiche le prix de votre sélection. Chaque option ajustée déclenche un nouveau calcul. Vous pourrez relire le montant et les conditions avant le paiement."),
                    new SitePageBlock("contact", "link", "Un besoin différent ?",
                        "Si les choix proposés ne correspondent pas à votre situation, expliquez-nous ce dont vous avez besoin.",
                        "/contact", "Nous écrire")
                ]),
            [("public", "/tarifs")] = new(
                ["tariffs_intro", "tariffs_catalog", "tariffs_explanations",
                 "tariffs_contact"],
                ["tariffs_faq", "tariffs_related"],
                ["tariffs_intro", "tariffs_catalog", "tariffs_explanations",
                 "tariffs_faq", "tariffs_related", "tariffs_contact"]),
            [("public", "/services")] = new(
                ["services_intro", "services_needs", "services_categories",
                 "services_contact"],
                ["services_explanations", "services_faq"],
                ["services_intro", "services_needs", "services_categories",
                 "services_explanations", "services_faq", "services_contact"]),
            [("public", "/diagnostic")] = new(
                ["diagnostic_questionnaire", "diagnostic_result"],
                [],
                InitialBlocks:
                [
                    new SitePageBlock("intro", "diagnostic_intro", "Faites le point, simplement.",
                        "Quelques questions concrètes pour identifier vos points solides et les priorités à examiner.",
                        Items:
                        [
                            new("Adapté à votre situation", "Particulier, activité professionnelle ou association.", null),
                            new("Résultat immédiat", "Des priorités lisibles, sans jargon inutile.", null)
                        ]),
                    new SitePageBlock("questionnaire", "widget", WidgetKey: "diagnostic_questionnaire"),
                    new SitePageBlock("result", "widget", WidgetKey: "diagnostic_result")
                ]),
            [("public", "/contact")] = new(
                ["contact_back", "contact_offer", "contact_form"],
                [],
                InitialBlocks:
                [
                    new SitePageBlock("back", "widget", WidgetKey: "contact_back"),
                    new SitePageBlock("intro", "contact_intro", "Parlons de votre besoin",
                        "Décrivez votre situation en quelques lignes : un problème à résoudre, un projet à lancer ou une question à poser. Nous vous aiderons à trouver la prochaine étape."),
                    new SitePageBlock("offer", "widget", WidgetKey: "contact_offer"),
                    new SitePageBlock("form", "widget", WidgetKey: "contact_form"),
                    new SitePageBlock("steps", "contact_steps", "Ce qui se passe ensuite", Items:
                    [
                        new("Vous envoyez votre message", "Nous recevons votre demande par e-mail.", null),
                        new("Nous vous répondons", "Nous utilisons l'adresse indiquée pour vous répondre et préciser votre besoin.", null),
                        new("Nous convenons de la suite", "Si votre demande nécessite une étude, nous vous expliquons les points à vérifier avant de proposer un devis.", null)
                    ]),
                    new SitePageBlock("related", "cards", "Vous préférez commencer autrement ?", Items:
                    [
                        new("Trouver une première orientation", "Quelques questions peuvent vous aider à situer votre besoin.", "/diagnostic", "Faire le questionnaire"),
                        new("Découvrir les services", "Parcourez les solutions proposées à la maison ou au travail.", "/services", "Voir les services")
                    ])
                ]),
            [("public", "/demander-mes-donnees")] = new(
                ["data_rights_actions"],
                [],
                InitialBlocks:
                [
                    new SitePageBlock("intro", "data_rights_intro", "Vos données, vos choix",
                        "Vous pouvez demander à consulter, corriger ou supprimer les données personnelles qui vous concernent, ainsi qu’exercer vos autres droits."),
                    new SitePageBlock("actions", "widget", WidgetKey: "data_rights_actions"),
                    new SitePageBlock("steps", "cards", "Comment se passe votre demande ?", Items:
                    [
                        new("Décrivez votre demande", "Connectez-vous et expliquez votre besoin en quelques mots.", null),
                        new("Suivez son traitement", "Consultez les échanges et les échéances dans votre espace client.", null),
                        new("Recevez la réponse", "Notre réponse et ses éventuels documents restent dans votre espace sécurisé.", null)
                    ]),
                    new SitePageBlock("deadline", "text", "Délais et vérification",
                        "Nous répondons dans les meilleurs délais, en principe sous un mois. Si nous avons un doute raisonnable sur votre identité, nous demanderons uniquement les éléments nécessaires pour la confirmer."),
                    new SitePageBlock("privacy", "link", "En savoir plus",
                        "Consultez les informations sur la manière dont nous traitons vos données.",
                        "/politique-confidentialite", "Lire la politique de confidentialité")
                ]),
            [("public", "/signup")] = new(
                ["signup_intro", "signup_selection", "signup_form", "signup_login"],
                ["signup_continuation", "signup_steps"],
                ["signup_intro", "signup_continuation", "signup_selection",
                 "signup_form", "signup_steps", "signup_login"]),
            [("client", "/souscrire")] = new(
                ["subscribe_intro", "subscribe_offers", "subscribe_direct"],
                ["subscribe_help"]),
            [("client", "/dashboard")] = new(
                ["client_home_intro", "client_home_status", "client_home_services",
                 "client_home_source"],
                ["client_home_metrics", "client_home_recent", "client_home_activity"],
                ["client_home_intro", "client_home_status", "client_home_metrics",
                 "client_home_services", "client_home_recent", "client_home_activity",
                 "client_home_source"]),
            [("client", "/profile")] = new(
                ["profile_intro", "profile_contact", "profile_security", "profile_source"],
                []),
            [("client", "/profile/donnees")] = new(
                ["data_request_intro", "data_request_form", "data_request_history"],
                ["data_request_help"]),
            [("client", "/profile/donnees/[id]")] = new(
                ["client_data_detail_intro", "client_data_detail_summary",
                 "client_data_detail_messages", "client_data_detail_file",
                 "client_data_detail_reply"],
                ["client_data_detail_help"]),
            [("admin", "/admin/data-requests")] = new(
                ["admin_data_request_intro", "admin_data_request_list"],
                ["admin_data_request_help"]),
            [("admin", "/admin")] = new(
                ["admin_home_intro", "admin_home_activity", "admin_home_overview",
                 "admin_home_integrations", "admin_home_shortcuts",
                 "admin_home_source"],
                []),
            [("admin", "/admin/catalog")] = new(
                ["admin_catalog_intro", "admin_catalog_vitrine", "admin_catalog_editor"],
                []),
            [("admin", "/admin/data-requests/[id]")] = new(
                ["admin_data_detail_intro", "admin_data_detail_summary",
                 "admin_data_detail_messages", "admin_data_detail_reply",
                 "admin_data_detail_file"],
                ["admin_data_detail_help"])
        };
    private readonly SqlRuntimeConfiguration _sql;
    private readonly MockSitePageStore _mock;
    private volatile bool _schemaReady;

    public SitePageService(SqlRuntimeConfiguration sql, MockSitePageStore mock)
    {
        _sql = sql;
        _mock = mock;
    }

    public bool IsPersistent => _sql.IsPersistent;

    public async Task<SitePageLayout> GetAsync(string area, string pageKey, CancellationToken token)
    {
        ValidatePageIdentity(area, pageKey);
        var key = StorageKey(area, pageKey);
        if (!IsPersistent)
            return _mock.Pages.TryGetValue(key, out var page)
                ? page with { Blocks = CompatibleBlocks(area, pageKey, page.Blocks) }
                : Default(area, pageKey);

        await using var connection = await OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT document_json, version, updated_at
            FROM site_page_layouts WHERE page_key = @key AND area = @area LIMIT 1;
            """;
        command.Parameters.AddWithValue("@key", key);
        command.Parameters.AddWithValue("@area", area);
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) return Default(area, pageKey);
        return new SitePageLayout(pageKey, area, reader.GetInt64("version"),
            CompatibleBlocks(area, pageKey, Deserialize(reader.GetString("document_json"))),
            Utc(reader.GetDateTime("updated_at")));
    }

    public async Task<SitePageLayout?> SaveAsync(
        SitePageMutationPayload payload, string actorUserId, CancellationToken token)
    {
        ValidatePageIdentity(payload.Area, payload.PageKey);
        ValidateBlocks(payload.Area, payload.PageKey, payload.Blocks);
        foreach (var mediaId in payload.Blocks.Select(block => block.MediaId).Where(id => id is not null).Distinct())
        {
            if (await GetMediaAsync(mediaId!, token) is null)
                throw new PortalValidationException();
        }
        if (payload.ExpectedVersion < 0) throw new PortalValidationException();
        var key = StorageKey(payload.Area, payload.PageKey);
        var now = DateTime.UtcNow;
        var nextVersion = payload.ExpectedVersion + 1;
        var next = new SitePageLayout(payload.PageKey, payload.Area,
            nextVersion, payload.Blocks, now.ToString("O"));
        if (!IsPersistent)
        {
            lock (_mock.Gate)
            {
                var current = _mock.Pages.TryGetValue(key, out var existing) ? existing.Version : 0;
                if (current != payload.ExpectedVersion) return null;
                _mock.Pages[key] = next;
                var revisions = _mock.Revisions.GetOrAdd(key, _ => []);
                if (current == 0)
                {
                    revisions.Add(new SitePageRevision(payload.PageKey, 0, now.ToString("O"), actorUserId));
                    _mock.Snapshots.GetOrAdd(key, _ => [])[0] = Default(payload.Area, payload.PageKey).Blocks;
                }
                revisions.Add(new SitePageRevision(payload.PageKey, nextVersion, now.ToString("O"), actorUserId));
                _mock.Snapshots.GetOrAdd(key, _ => [])[nextVersion] =
                    JsonSerializer.Deserialize<List<SitePageBlock>>(JsonSerializer.Serialize(payload.Blocks, Json), Json)!;
                return next;
            }
        }

        await using var connection = await OpenAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        long currentVersion;
        await using (var guard = connection.CreateCommand())
        {
            guard.Transaction = transaction;
            guard.CommandText = "SELECT version FROM site_page_layouts WHERE page_key = @key FOR UPDATE;";
            guard.Parameters.AddWithValue("@key", key);
            var scalar = await guard.ExecuteScalarAsync(token);
            currentVersion = scalar is null ? 0 : Convert.ToInt64(scalar);
        }
        if (currentVersion != payload.ExpectedVersion)
        {
            await transaction.RollbackAsync(token);
            return null;
        }
        var documentJson = JsonSerializer.Serialize(payload.Blocks, Json);
        await using (var write = connection.CreateCommand())
        {
            write.Transaction = transaction;
            write.CommandText = currentVersion == 0
                ? """
                  INSERT INTO site_page_layouts
                    (page_key, area, document_json, version, updated_at, updated_by)
                  VALUES (@key, @area, @json, @version, @now, @actor);
                  """
                : """
                  UPDATE site_page_layouts
                  SET document_json = @json, version = @version, updated_at = @now, updated_by = @actor
                  WHERE page_key = @key AND area = @area;
                  """;
            write.Parameters.AddWithValue("@key", key);
            write.Parameters.AddWithValue("@area", payload.Area);
            write.Parameters.AddWithValue("@json", documentJson);
            write.Parameters.AddWithValue("@version", nextVersion);
            write.Parameters.AddWithValue("@now", now);
            write.Parameters.AddWithValue("@actor", actorUserId);
            try
            {
                await write.ExecuteNonQueryAsync(token);
            }
            catch (MySqlException exception) when (exception.Number == 1062)
            {
                await transaction.RollbackAsync(token);
                return null;
            }
        }
        if (currentVersion == 0)
        {
            await using var baseline = connection.CreateCommand();
            baseline.Transaction = transaction;
            baseline.CommandText = """
                INSERT INTO site_page_layout_revisions
                    (page_key, version, document_json, created_at, created_by)
                VALUES (@key, 0, @json, @now, @actor);
                """;
            baseline.Parameters.AddWithValue("@key", key);
            baseline.Parameters.AddWithValue("@json", JsonSerializer.Serialize(Default(payload.Area, payload.PageKey).Blocks, Json));
            baseline.Parameters.AddWithValue("@now", now);
            baseline.Parameters.AddWithValue("@actor", actorUserId);
            await baseline.ExecuteNonQueryAsync(token);
        }
        await using (var revision = connection.CreateCommand())
        {
            revision.Transaction = transaction;
            revision.CommandText = """
                INSERT INTO site_page_layout_revisions
                    (page_key, version, document_json, created_at, created_by)
                VALUES (@key, @version, @json, @now, @actor);
                """;
            revision.Parameters.AddWithValue("@key", key);
            revision.Parameters.AddWithValue("@version", nextVersion);
            revision.Parameters.AddWithValue("@json", documentJson);
            revision.Parameters.AddWithValue("@now", now);
            revision.Parameters.AddWithValue("@actor", actorUserId);
            await revision.ExecuteNonQueryAsync(token);
        }
        await transaction.CommitAsync(token);
        return next;
    }

    public async Task<IReadOnlyList<SitePageRevision>> RevisionsAsync(
        string area, string pageKey, CancellationToken token)
    {
        ValidatePageIdentity(area, pageKey);
        var key = StorageKey(area, pageKey);
        if (!IsPersistent)
            return _mock.Revisions.TryGetValue(key, out var items)
                ? items.OrderByDescending(item => item.Version).ToArray() : [];
        await using var connection = await OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT version, created_at, created_by
            FROM site_page_layout_revisions WHERE page_key = @key
            ORDER BY version DESC LIMIT 50;
            """;
        command.Parameters.AddWithValue("@key", key);
        var revisions = new List<SitePageRevision>();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
            revisions.Add(new SitePageRevision(pageKey, reader.GetInt64("version"),
                Utc(reader.GetDateTime("created_at")),
                Kermaria.ApiInternal.Data.Repositories.MariaDbIdentifierReader.ReadRequired(reader, "created_by")));
        return revisions;
    }

    public async Task<SitePageLayout?> RestoreAsync(
        string area, string pageKey, long version, long expectedVersion,
        string actorUserId, CancellationToken token)
    {
        ValidatePageIdentity(area, pageKey);
        var key = StorageKey(area, pageKey);
        IReadOnlyList<SitePageBlock>? blocks = null;
        if (!IsPersistent)
        {
            if (_mock.Snapshots.TryGetValue(key, out var snapshots)
                && snapshots.TryGetValue(version, out var saved))
                blocks = saved;
        }
        else
        {
            await using var connection = await OpenAsync(token);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT document_json FROM site_page_layout_revisions
                WHERE page_key = @key AND version = @version LIMIT 1;
                """;
            command.Parameters.AddWithValue("@key", key);
            command.Parameters.AddWithValue("@version", version);
            var json = await command.ExecuteScalarAsync(token) as string;
            if (json is not null) blocks = Deserialize(json);
        }
        return blocks is null ? null : await SaveAsync(
            new SitePageMutationPayload(pageKey, area, expectedVersion,
                CompatibleBlocks(area, pageKey, blocks)),
            actorUserId, token);
    }

    public async Task<IReadOnlyList<SiteMediaAsset>> ListMediaAsync(CancellationToken token)
    {
        if (!IsPersistent)
            return _mock.Media.Values.Select(item => item.Metadata)
                .OrderByDescending(item => item.CreatedAt).ToArray();
        await using var connection = await OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, file_name, content_type, alt_text, byte_length, created_at
            FROM site_media_assets ORDER BY created_at DESC LIMIT 200;
            """;
        var assets = new List<SiteMediaAsset>();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
            assets.Add(ReadMedia(reader));
        return assets;
    }

    public async Task<SiteMediaAsset> UploadMediaAsync(
        string fileName, string altText, string contentType, byte[] data,
        string actorUserId, CancellationToken token)
    {
        altText = altText.Trim();
        fileName = Path.GetFileName(fileName).Trim();
        if (altText.Length is < 3 or > 240 || fileName.Length is < 1 or > 180
            || data.Length is < 20 or > MaxImageBytes || !ValidImage(contentType, data))
            throw new PortalValidationException();
        var id = Guid.NewGuid().ToString("D");
        var now = DateTime.UtcNow;
        var asset = new SiteMediaAsset(id, fileName, contentType, altText,
            data.Length, now.ToString("O"));
        if (!IsPersistent)
        {
            _mock.Media[id] = (asset, data);
            return asset;
        }
        await using var connection = await OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO site_media_assets
                (id, file_name, content_type, alt_text, byte_length, data, created_at, created_by)
            VALUES (@id, @name, @type, @alt, @length, @data, @now, @actor);
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@name", fileName);
        command.Parameters.AddWithValue("@type", contentType);
        command.Parameters.AddWithValue("@alt", altText);
        command.Parameters.AddWithValue("@length", data.Length);
        command.Parameters.AddWithValue("@data", data);
        command.Parameters.AddWithValue("@now", now);
        command.Parameters.AddWithValue("@actor", actorUserId);
        await command.ExecuteNonQueryAsync(token);
        return asset;
    }

    public async Task<(SiteMediaAsset Metadata, byte[] Data)?> GetMediaAsync(
        string id, CancellationToken token)
    {
        if (!Guid.TryParse(id, out _)) return null;
        if (!IsPersistent)
            return _mock.Media.TryGetValue(id, out var value) ? value : null;
        await using var connection = await OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, file_name, content_type, alt_text, byte_length, data, created_at
            FROM site_media_assets WHERE id = @id LIMIT 1;
            """;
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token)
            ? (ReadMedia(reader), (byte[])reader["data"]) : null;
    }

    private static SiteMediaAsset ReadMedia(MySqlDataReader reader)
        => new(Kermaria.ApiInternal.Data.Repositories.MariaDbIdentifierReader.ReadRequired(reader, "id"), reader.GetString("file_name"),
            reader.GetString("content_type"), reader.GetString("alt_text"),
            reader.GetInt32("byte_length"), Utc(reader.GetDateTime("created_at")));

    private static bool ValidImage(string type, byte[] data)
        => type switch
        {
            "image/png" => data.Length > 8 && data.AsSpan(0, 8).SequenceEqual(
                new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            "image/jpeg" => data.Length > 3 && data[0] == 255 && data[1] == 216 && data[2] == 255,
            "image/webp" => data.Length > 12 &&
                System.Text.Encoding.ASCII.GetString(data, 0, 4) == "RIFF" &&
                System.Text.Encoding.ASCII.GetString(data, 8, 4) == "WEBP",
            _ => false
        };

    private static void ValidatePageIdentity(string area, string pageKey)
    {
        if (area is not ("public" or "client" or "admin")
            || pageKey.Length is < 1 or > 200
            || !PageKeyPattern().IsMatch(pageKey)
            || (area == "admin" && pageKey != "/admin"
                && !pageKey.StartsWith("/admin/", StringComparison.Ordinal))
            || (area != "admin" && pageKey.StartsWith("/admin", StringComparison.Ordinal)))
            throw new PortalValidationException();
    }

    private static void ValidateBlocks(string area, string pageKey, IReadOnlyList<SitePageBlock>? blocks)
    {
        var isFooter = area == "public" && pageKey == "/footer";
        var isHome = area == "public" && pageKey == "/";
        var isDiagnostic = area == "public" && pageKey == "/diagnostic";
        var isContact = area == "public" && pageKey == "/contact";
        var isDataRights = area == "public" && pageKey == "/demander-mes-donnees";
        WidgetPages.TryGetValue((area, pageKey), out var widgetRule);
        var isWidgetPage = widgetRule is not null;
        if (blocks is null || blocks.Count is < 1 or > MaxBlocks
            || (!isFooter && !isHome && !isWidgetPage
                && blocks.Count(block => block.Type == "route_content") != 1)
            || (isWidgetPage && (blocks.Any(block => block.Type == "route_content")
                || blocks.Where(block => block.Type == "widget")
                    .Select(block => block.WidgetKey).Distinct(StringComparer.Ordinal).Count()
                    != blocks.Count(block => block.Type == "widget")
                || widgetRule!.Required
                    .Any(key => blocks.Count(block => block.Type == "widget" && block.WidgetKey == key) != 1)))
            || (isDiagnostic && blocks.Count(block => block.Type == "diagnostic_intro") != 1)
            || (isContact && blocks.Count(block => block.Type == "contact_intro") != 1)
            || (isDataRights && blocks.Count(block => block.Type == "data_rights_intro") != 1)
            || (isHome && (blocks.Count(block => block.Type == "hero") != 1
                || blocks.Any(block => block.Type == "route_content")
                || !blocks.Any(block => block.Href == "/contact"
                    || block.Items?.Any(item => item.Href == "/contact") == true)))
            || (isFooter && (blocks.Count(block => block.Type == "footer_brand") != 1
                || blocks.Count(block => block.Type == "footer_links") < 1
                || !new[] { "/mentions-legales", "/politique-confidentialite", "/cgv", "/demander-mes-donnees" }
                    .All(href => blocks.SelectMany(block => block.Items ?? []).Any(item => item.Href == href))))
            || blocks.Select(block => block.Id).Distinct(StringComparer.Ordinal).Count() != blocks.Count)
            throw new PortalValidationException();
        foreach (var block in blocks)
        {
            if (!BlockTypes.Contains(block.Type) || block.Id.Length is < 1 or > 80
                || (!isFooter && block.Type is "footer_brand" or "footer_links")
                || (isFooter && block.Type is not ("footer_brand" or "footer_links"))
                || (!isHome && block.Type is "hero" or "audiences" or "steps" or "services" or "offer_path" or "final_cta")
                || (block.Type == "offers_story" && (area != "public" || pageKey != "/offres"))
                || (block.Type == "diagnostic_intro" && !isDiagnostic)
                || (block.Type is "contact_intro" or "contact_steps" && !isContact)
                || (block.Type == "data_rights_intro" && !isDataRights)
                || (block.Type == "widget" && (widgetRule is null
                    || !widgetRule.Required.Concat(widgetRule.Optional)
                        .Contains(block.WidgetKey, StringComparer.Ordinal)))
                || block.Title?.Length > 200 || block.Body?.Length > 5000
                || block.Label?.Length > 100 || block.Items?.Count > 12
                || block.Items?.Any(item => item.Title.Length > 160 || item.Body?.Length > 1000 || item.Label?.Length > 100) == true)
                throw new PortalValidationException();
            if (block.Href is not null && !ValidHref(block.Href)) throw new PortalValidationException();
            if (block.Items?.Any(item => item.Href is not null && !ValidHref(item.Href)) == true)
                throw new PortalValidationException();
            var editableTexts = new[] { block.Title, block.Body, block.Label }
                .Concat((block.Items ?? []).SelectMany(item => new[] { item.Title, item.Body, item.Label }));
            if (editableTexts.Any(text => text is not null &&
                Regex.IsMatch(text,
                    @"(?:(?<!\w)\d[\d\s,.]*\s*(?:€|EUR\b|euros?\b)|(?:€|EUR\b|euros?\b)\s*\d)",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)))
                throw new PortalValidationException();
            if (block.Type == "image" && !Guid.TryParse(block.MediaId, out _))
                throw new PortalValidationException();
            if (block.MediaId is not null && !Guid.TryParse(block.MediaId, out _))
                throw new PortalValidationException();
            if (block.Type == "form" && (block.Action is null
                || !FormActions.Contains(block.Action)
                || (block.Action == "data_request" && area != "client")
                || string.IsNullOrWhiteSpace(block.Title)
                || string.IsNullOrWhiteSpace(block.Label)))
                throw new PortalValidationException();
            if (block.Fields is { Count: > 0 })
            {
                if (block.Type != "form" || block.Fields.Count > 8
                    || block.Fields.Select(field => field.Id).Distinct(StringComparer.Ordinal).Count() != block.Fields.Count)
                    throw new PortalValidationException();
                foreach (var field in block.Fields)
                {
                    if (!Regex.IsMatch(field.Id, "^[a-z][a-z0-9_]{0,31}$", RegexOptions.CultureInvariant)
                        || field.Id is "name" or "email" or "subject" or "message" or "details" or "requestType"
                        || field.Label.Length is < 3 or > 100
                        || !FormFieldTypes.Contains(field.Type)
                        || (field.Type == "select" && (field.Options is null || field.Options.Count is < 1 or > 10))
                        || field.Options?.Any(option => option.Length is < 1 or > 80) == true)
                        throw new PortalValidationException();
                }
            }
            var requiresItems = block.Type is "cards" or "faq" or "audiences"
                or "steps" or "services" or "offer_path" or "footer_links";
            if (requiresItems && (block.Items is null || block.Items.Count == 0
                || block.Items.Any(item => string.IsNullOrWhiteSpace(item.Title))))
                throw new PortalValidationException();
            if (block.Type is "hero" or "footer_brand" or "final_cta" or "link"
                && (string.IsNullOrWhiteSpace(block.Title)
                    || string.IsNullOrWhiteSpace(block.Label)
                    || string.IsNullOrWhiteSpace(block.Href)))
                throw new PortalValidationException();
            if (block.Type == "offers_story" && (string.IsNullOrWhiteSpace(block.Title)
                || string.IsNullOrWhiteSpace(block.Body)
                || string.IsNullOrWhiteSpace(block.Label)
                || string.IsNullOrWhiteSpace(block.Href)
                || block.Items is null || block.Items.Count is < 1 or > 6
                || block.Items.Any(item => string.IsNullOrWhiteSpace(item.Title)
                    || string.IsNullOrWhiteSpace(item.Body))))
                throw new PortalValidationException();
            if (block.Type == "diagnostic_intro" && (string.IsNullOrWhiteSpace(block.Title)
                || string.IsNullOrWhiteSpace(block.Body)
                || block.Items is null || block.Items.Count is < 1 or > 3
                || block.Items.Any(item => string.IsNullOrWhiteSpace(item.Title)
                    || string.IsNullOrWhiteSpace(item.Body))))
                throw new PortalValidationException();
            if (block.Type == "contact_intro" && (string.IsNullOrWhiteSpace(block.Title)
                || string.IsNullOrWhiteSpace(block.Body)))
                throw new PortalValidationException();
            if (block.Type == "data_rights_intro" && (string.IsNullOrWhiteSpace(block.Title)
                || string.IsNullOrWhiteSpace(block.Body)))
                throw new PortalValidationException();
            if (block.Type == "contact_steps" && (string.IsNullOrWhiteSpace(block.Title)
                || block.Items is null || block.Items.Count is < 1 or > 5
                || block.Items.Any(item => string.IsNullOrWhiteSpace(item.Title)
                    || string.IsNullOrWhiteSpace(item.Body))))
                throw new PortalValidationException();
            if (block.Type == "hero" && string.IsNullOrWhiteSpace(block.Body))
                throw new PortalValidationException();
            if (block.Type == "image" && string.IsNullOrWhiteSpace(block.Label))
                throw new PortalValidationException();
        }
    }

    private static bool ValidHref(string href)
        => href.Length <= 240 && href.StartsWith('/') && !href.StartsWith("//")
            && !href.Contains('\\') && !href.Contains('?') && !href.Contains('#');

    private static IReadOnlyList<SitePageBlock> CompatibleBlocks(
        string area, string pageKey, IReadOnlyList<SitePageBlock> blocks)
    {
        if (!WidgetPages.ContainsKey((area, pageKey))
            || blocks.Count(block => block.Type == "route_content") != 1
            || blocks.Any(block => block.Type == "widget"))
            return blocks;

        var usedIds = blocks.Select(block => block.Id).ToHashSet(StringComparer.Ordinal);
        var converted = new List<SitePageBlock>(blocks.Count + 8);
        foreach (var block in blocks)
        {
            if (block.Type != "route_content")
            {
                converted.Add(block);
                continue;
            }
            foreach (var initial in Default(area, pageKey).Blocks)
            {
                var id = initial.Id;
                if (usedIds.Contains(id))
                {
                    var suffix = 0;
                    do { id = $"core-{initial.Id}-{++suffix}"; }
                    while (usedIds.Contains(id));
                }
                usedIds.Add(id);
                converted.Add(initial with { Id = id });
            }
        }
        return converted;
    }

    private static SitePageLayout Default(string area, string pageKey)
    {
        if (WidgetPages.TryGetValue((area, pageKey), out var rule))
            return new SitePageLayout(pageKey, area, 0,
                rule.InitialBlocks ?? (rule.Initial ?? rule.Required)
                    .Select(key => new SitePageBlock(key, "widget", WidgetKey: key)).ToArray(),
                null);

        return area == "public" && pageKey == "/"
            ? new(pageKey, area, 0,
            [
                new SitePageBlock("hero", "hero", "Une informatique fiable, simplement.",
                    "Dépannage, installation, protection de vos fichiers et conseils : Zachary IT vous accompagne à la maison comme au travail.",
                    "/services", "Trouver une solution", Items:
                    [new("Demander un conseil", null, "/contact")]),
                new SitePageBlock("audiences", "audiences", "Quelle est votre situation ?",
                    "Choisissez le point de départ qui vous ressemble.", Items:
                    [
                        new("Particuliers", "Pour retrouver un ordinateur agréable à utiliser, un Wi-Fi fiable et des fichiers importants protégés.", "/services"),
                        new("Associations", "Pour partager les informations plus facilement et continuer à fonctionner quand un bénévole ou un outil manque.", "/services"),
                        new("Indépendants et petites entreprises", "Pour travailler sereinement avec des outils suivis, des données protégées et un interlocuteur disponible.", "/services")
                    ]),
                new SitePageBlock("steps", "steps", "Trois étapes pour définir et mettre en place votre solution.", Items:
                    [
                        new("Vous nous expliquez votre besoin", "Une question, une panne ou un projet : décrivez simplement votre situation, même si vous ne savez pas quelle solution choisir.", null),
                        new("Nous proposons une réponse claire", "Vous savez ce qui est prévu, ce que cela coûte et ce qui se passe ensuite avant de prendre une décision.", null),
                        new("Nous restons à vos côtés", "Une fois la solution mise en place, nous vérifions qu'elle vous convient et restons disponibles si votre besoin évolue.", null)
                    ]),
                new SitePageBlock("services", "services", "De quoi avez-vous besoin ?",
                    "Partez de votre problème ou de votre projet. Nous vous aiderons à trouver l'offre adaptée, sans avoir à connaître les termes techniques.", Items:
                    [
                        new("Un réseau qui fonctionne", "Retrouvez une connexion stable à la maison ou au travail, là où vous en avez besoin.", null),
                        new("Des outils prêts à l'emploi", "Installation, aide à la prise en main et accès à vos outils, y compris à distance lorsque c'est utile.", null),
                        new("Vos fichiers protégés", "Gardez une copie de vos documents importants et préparez leur récupération en cas de problème.", null),
                        new("Votre activité en ligne", "Site, adresse e-mail et services en ligne : nous vous aidons à les mettre en place et à les suivre.", null),
                        new("Une aide quand il faut", "Obtenez une réponse quand un outil bloque, et un suivi pour éviter que les problèmes s'accumulent.", null)
                    ]),
                new SitePageBlock("offer", "offer_path", "Choisissez votre prochaine étape",
                    "Comparez les solutions proposées ou posez votre question directement.", Items:
                    [
                        new("Je veux comparer les offres", "Découvrez à quoi elles servent et ce qu'elles comprennent.", "/offres", "Voir les offres"),
                        new("Je veux connaître les tarifs", "Consultez les prix affichés et les prestations proposées sur devis.", "/tarifs", "Voir les tarifs"),
                        new("Je ne sais pas encore", "Quelques questions simples vous aideront à situer votre besoin.", "/diagnostic", "M'orienter")
                    ]),
                new SitePageBlock("contact", "final_cta", "Un projet ou une question ? Parlons-en.",
                    "Expliquez votre besoin en quelques mots. Nous vous répondrons avec une prochaine étape claire, sans vous demander de choisir seul une solution.",
                    "/contact", "Parler de mon besoin", Items:
                    [new("Comparer les offres", null, "/offres")])
            ], null)
            : area == "public" && pageKey == "/footer"
            ? new(pageKey, area, 0,
            [
                new SitePageBlock("brand", "footer_brand", "Zachary IT",
                    "Une aide claire pour votre informatique, à la maison comme au travail.",
                    "/contact", "Parler de mon besoin"),
                new SitePageBlock("services", "footer_links", "Services", Items:
                [
                    new("Tous les services", null, "/services"),
                    new("Assistance", null, "/services/support-it"),
                    new("Réseau et Wi-Fi", null, "/services/reseau-securite"),
                    new("Site et messagerie", null, "/services/domaines-messagerie")
                ]),
                new SitePageBlock("discover", "footer_links", "Découvrir", Items:
                [
                    new("Offres", null, "/offres"), new("Tarifs", null, "/tarifs"),
                    new("Trouver ma solution", null, "/diagnostic"),
                    new("À propos", null, "/a-propos"),
                    new("Notre fonctionnement", null, "/infrastructure")
                ]),
                new SitePageBlock("help", "footer_links", "Aide et informations", Items:
                [
                    new("Nous contacter", null, "/contact"),
                    new("Espace client", null, "/login"),
                    new("Mentions légales", null, "/mentions-legales"),
                    new("Politique de confidentialité", null, "/politique-confidentialite"),
                    new("Demander mes données", null, "/demander-mes-donnees"),
                    new("Conditions générales de vente", null, "/cgv")
                ])
            ], null)
            : new(pageKey, area, 0, [new SitePageBlock("main", "route_content")], null);
    }

    private static string StorageKey(string area, string pageKey) => area + ":" + pageKey;
    private static string Utc(DateTime value)
        => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("O");
    private static IReadOnlyList<SitePageBlock> Deserialize(string json)
        => JsonSerializer.Deserialize<List<SitePageBlock>>(json, Json)
            ?? throw new InvalidOperationException("Invalid page layout document.");
    private async Task<MySqlConnection> OpenAsync(CancellationToken token)
    {
        var connection = new MySqlConnection(_sql.ConnectionString);
        await connection.OpenAsync(token);
        if (!_schemaReady)
        {
            await using var schema = connection.CreateCommand();
            schema.CommandText = """
            SELECT COUNT(*) FROM information_schema.tables
            WHERE table_schema = DATABASE()
              AND table_name IN ('site_page_layouts', 'site_page_layout_revisions', 'site_media_assets');
            """;
            if (Convert.ToInt32(await schema.ExecuteScalarAsync(token)) != 3)
            {
                await connection.DisposeAsync();
                throw new SiteFeatureSchemaUnavailableException("le constructeur de pages (migration 100)");
            }
            _schemaReady = true;
        }
        return connection;
    }

    [GeneratedRegex("^/[a-z0-9/_\\[\\].-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex PageKeyPattern();
}
