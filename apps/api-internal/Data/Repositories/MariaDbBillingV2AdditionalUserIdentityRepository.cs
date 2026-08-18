using System.Globalization;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;
using MySqlConnector;

namespace Kermaria.ApiInternal.Data.Repositories;

/// <summary>
/// Cycle de vie d'identite des places USER-ADDITIONAL, en base reelle.
/// </summary>
/// <remarks>
/// <para>
/// L'attribution est integralement transactionnelle. Elle verrouille la place
/// (<c>FOR UPDATE</c>) avant toute lecture de decision : sans ce verrou, deux
/// requetes concurrentes liraient toutes les deux
/// <c>identity_reference IS NULL</c> et creeraient deux utilisateurs portail
/// pour une seule place vendue.
/// </para>
/// <para>
/// Les index uniques posent le dernier mot : <c>portal_users.email</c>,
/// <c>portal_users.koxo_unique_identifier</c>,
/// <c>uq_billing_v2_user_identity_slot</c> et
/// <c>uq_billing_v2_user_identity_portal_user</c>. Une violation d'unicite est
/// traduite en refus, jamais laissee remonter en erreur serveur : c'est le
/// resultat normal d'une course perdue.
/// </para>
/// </remarks>
public sealed class MariaDbBillingV2AdditionalUserIdentityRepository
    : IBillingV2AdditionalUserIdentityRepository
{
    /// <summary>Code MySQL d'une violation de contrainte d'unicite.</summary>
    private const int DuplicateEntryErrorNumber = 1062;

    private readonly string _connectionString;

    public MariaDbBillingV2AdditionalUserIdentityRepository(
        SqlRuntimeConfiguration configuration)
    {
        _connectionString = configuration.ConnectionString
            ?? throw new InvalidOperationException(
                "MariaDB connection configuration is unavailable.");
    }

    public bool IsPersistent => true;

    public async Task<BillingV2AdditionalUserAssignmentResult> AssignAsync(
        BillingV2AdditionalUserAssignmentCommand command,
        Func<BillingV2AdditionalUserSlotSnapshot, string?> validate,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            cancellationToken);

        try
        {
            var snapshot = await LoadSlotForUpdateAsync(
                connection,
                transaction,
                command,
                cancellationToken);
            if (snapshot is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return BillingV2AdditionalUserAssignmentResult.Reject(
                    BillingV2AdditionalUserRejectionCodes.SlotNotFound);
            }

            var rejection = validate(snapshot);
            if (rejection is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return BillingV2AdditionalUserAssignmentResult.Reject(
                    rejection);
            }

            var koxoUniqueIdentifier = await KoxoIdentifierAllocator
                .AllocateAsync(connection, transaction, cancellationToken);

            await InsertPortalUserAsync(
                connection,
                transaction,
                command,
                koxoUniqueIdentifier,
                cancellationToken);

            // Le UPDATE est conditionne a `identity_reference IS NULL` en plus
            // du verrou : si une seule ligne n'est pas affectee, la place a
            // change sous nos pieds et la transaction entiere est annulee.
            var slotAffected = await AssignSlotAsync(
                connection,
                transaction,
                command,
                cancellationToken);
            if (slotAffected != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return BillingV2AdditionalUserAssignmentResult.Reject(
                    BillingV2AdditionalUserRejectionCodes.SlotAlreadyAssigned);
            }

            await InsertLifecycleAsync(
                connection,
                transaction,
                command,
                koxoUniqueIdentifier,
                cancellationToken);

            await InsertPasswordSetupAsync(
                connection,
                transaction,
                command,
                cancellationToken);

            await InsertAuditAsync(
                connection,
                transaction,
                command,
                koxoUniqueIdentifier,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return BillingV2AdditionalUserAssignmentResult.Success(
                new BillingV2AdditionalUserIdentityRecord(
                    command.LifecycleId,
                    command.SubscriptionUserId,
                    command.SubscriptionId,
                    command.CustomerId,
                    snapshot.CustomerReference!,
                    command.PortalUserId,
                    koxoUniqueIdentifier,
                    BillingV2UserIdentityStatuses.AwaitingPassword,
                    FailureCode: null,
                    DirectoryObjectGuid: null,
                    command.NormalizedEmail,
                    command.DisplayName));
        }
        catch (MySqlException exception)
            when (exception.Number == DuplicateEntryErrorNumber)
        {
            await SafeRollbackAsync(transaction, cancellationToken);

            // Le verrou de place rend ce chemin improbable, pas impossible :
            // il ne couvre ni `portal_users.email` ni un cycle de vie insere
            // par une autre voie. Plutot que de deduire le conflit du nom de la
            // contrainte, on relit l'etat reel de la place, verrou relache.
            //
            // Une place desormais attribuee tranche : c'est le meme refus que
            // celui qu'aurait rendu la politique si la lecture verrouillee
            // etait arrivee un instant plus tard, et l'appelant obtient la
            // meme reponse quel que soit l'ordre d'arrivee.
            var identityReference = await ReadSlotIdentityReferenceAsync(
                connection,
                command.SubscriptionUserId,
                cancellationToken);
            if (identityReference is not null)
            {
                return BillingV2AdditionalUserAssignmentResult.Reject(
                    BillingV2AdditionalUserRejectionCodes.SlotAlreadyAssigned);
            }

            // La place est reellement libre : le conflit vient d'ailleurs. On
            // conserve alors le diagnostic specifique — un cycle de vie sans
            // place attribuee est un etat incoherent qu'il ne faut surtout pas
            // repeindre en banal conflit d'attribution.
            return BillingV2AdditionalUserAssignmentResult.Reject(
                ClassifyDuplicate(exception));
        }
    }

    /// <summary>
    /// Relit l'occupation de la place, hors transaction.
    /// </summary>
    private static async Task<string?> ReadSlotIdentityReferenceAsync(
        MySqlConnection connection,
        string subscriptionUserId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT identity_reference
            FROM billing_v2_subscription_users
            WHERE id = @subscription_user_id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue(
            "@subscription_user_id",
            subscriptionUserId);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull
            ? null
            : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Traduit une violation d'unicite en refus metier.
    /// </summary>
    /// <remarks>
    /// Le message porte le nom de la contrainte violee. Faute de le reconnaitre,
    /// on retombe sur le conflit de place : c'est le refus le plus restrictif,
    /// donc le seul sur lequel il est acceptable de se tromper.
    /// </remarks>
    private static string ClassifyDuplicate(MySqlException exception)
    {
        var message = exception.Message;
        if (message.Contains("portal_users.email", StringComparison.OrdinalIgnoreCase)
            || message.Contains("ux_portal_users_email", StringComparison.OrdinalIgnoreCase))
        {
            return BillingV2AdditionalUserRejectionCodes.EmailAlreadyUsed;
        }

        if (message.Contains(
                "uq_billing_v2_user_identity_portal_user",
                StringComparison.OrdinalIgnoreCase))
        {
            return BillingV2AdditionalUserRejectionCodes.LifecycleAlreadyExists;
        }

        return BillingV2AdditionalUserRejectionCodes.SlotAlreadyAssigned;
    }

    private static async Task SafeRollbackAsync(
        MySqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // Transaction deja terminee : rien a annuler.
        }
    }

    private static async Task<BillingV2AdditionalUserSlotSnapshot?>
        LoadSlotForUpdateAsync(
            MySqlConnection connection,
            MySqlTransaction transaction,
            BillingV2AdditionalUserAssignmentCommand command,
            CancellationToken cancellationToken)
    {
        string subscriptionId;
        string subscriptionCustomerId;
        string subscriptionStatus;
        bool isPrimary;
        string slotStatus;
        string? identityReference;

        await using (var slotCommand = connection.CreateCommand())
        {
            slotCommand.Transaction = transaction;
            slotCommand.CommandText =
                """
                SELECT
                    slot.subscription_id,
                    slot.is_primary,
                    slot.status AS slot_status,
                    slot.identity_reference,
                    sub.customer_id,
                    sub.status AS subscription_status
                FROM billing_v2_subscription_users slot
                INNER JOIN billing_v2_subscriptions sub
                    ON sub.id = slot.subscription_id
                WHERE slot.id = @subscription_user_id
                FOR UPDATE;
                """;
            slotCommand.Parameters.AddWithValue(
                "@subscription_user_id",
                command.SubscriptionUserId);

            await using var reader = await slotCommand.ExecuteReaderAsync(
                cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            subscriptionId = MariaDbIdentifierReader.ReadRequired(
                reader,
                "subscription_id");
            isPrimary = reader.GetBoolean("is_primary");
            slotStatus = reader.GetString("slot_status");
            identityReference =
                reader.IsDBNull(reader.GetOrdinal("identity_reference"))
                    ? null
                    : reader.GetString("identity_reference");
            subscriptionCustomerId = MariaDbIdentifierReader.ReadRequired(
                reader,
                "customer_id");
            subscriptionStatus = reader.GetString("subscription_status");
        }

        var customerReference = await ReadNullableScalarAsync(
            connection,
            transaction,
            """
            SELECT external_reference
            FROM customers
            WHERE id = @customer_id
              AND status = 'active'
            LIMIT 1;
            """,
            [("@customer_id", command.CustomerId)],
            cancellationToken);

        var entitlementCount = await ReadCountAsync(
            connection,
            transaction,
            """
            SELECT COUNT(*)
            FROM billing_v2_subscription_items item
            INNER JOIN billing_v2_services service
                ON service.id = item.service_id
               AND service.status = 'active'
            INNER JOIN billing_v2_provisioning_rules rule
                ON rule.service_id = service.id
               AND rule.status = 'active'
               AND rule.rule_type = 'contractual_entitlement'
               AND rule.target_type = 'user_slot'
               AND (rule.tier_id IS NULL OR rule.tier_id = item.tier_id)
            WHERE item.subscription_user_id = @subscription_user_id
              AND item.subscription_id = @subscription_id
              AND item.status = 'active'
              AND item.scope_type = 'user'
              AND item.effective_from <= UTC_TIMESTAMP(6)
              AND (item.effective_until IS NULL
                   OR item.effective_until > UTC_TIMESTAMP(6));
            """,
            [
                ("@subscription_user_id", command.SubscriptionUserId),
                ("@subscription_id", subscriptionId)
            ],
            cancellationToken);

        // Un item rattache a la place mais declare hors scope utilisateur
        // decrit une intention que le modele ne sait pas honorer : attribuer
        // une personne a une place ainsi cablee la rendrait titulaire d'un
        // droit dont le perimetre est indetermine.
        var incompatibleItemCount = await ReadCountAsync(
            connection,
            transaction,
            """
            SELECT COUNT(*)
            FROM billing_v2_subscription_items
            WHERE subscription_user_id = @subscription_user_id
              AND (scope_type <> 'user'
                   OR subscription_id <> @subscription_id);
            """,
            [
                ("@subscription_user_id", command.SubscriptionUserId),
                ("@subscription_id", subscriptionId)
            ],
            cancellationToken);

        var lifecycleCount = await ReadCountAsync(
            connection,
            transaction,
            """
            SELECT COUNT(*)
            FROM billing_v2_user_identity_provisioning
            WHERE subscription_user_id = @subscription_user_id;
            """,
            [("@subscription_user_id", command.SubscriptionUserId)],
            cancellationToken);

        var emailCount = await ReadCountAsync(
            connection,
            transaction,
            """
            SELECT COUNT(*)
            FROM portal_users
            WHERE email = @email;
            """,
            [("@email", command.NormalizedEmail)],
            cancellationToken);

        return new BillingV2AdditionalUserSlotSnapshot(
            command.SubscriptionUserId,
            subscriptionId,
            subscriptionCustomerId,
            subscriptionStatus,
            isPrimary,
            slotStatus,
            identityReference,
            customerReference,
            entitlementCount > 0,
            incompatibleItemCount,
            lifecycleCount > 0,
            emailCount > 0);
    }

    private static async Task InsertPortalUserAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        BillingV2AdditionalUserAssignmentCommand command,
        string koxoUniqueIdentifier,
        CancellationToken cancellationToken)
    {
        await using var userCommand = connection.CreateCommand();
        userCommand.Transaction = transaction;
        // status='active' et password_hash=NULL : c'est exactement l'etat que
        // l'inscription produit entre l'approbation et la definition du mot de
        // passe. `active` est indispensable a l'export KoXo, et l'absence de
        // condensat suffit a rendre le compte non connectable — le service
        // d'authentification compare alors contre le condensat factice.
        // Introduire un statut `pending` casserait les deux.
        userCommand.CommandText =
            """
            INSERT INTO portal_users (
                id,
                customer_id,
                identity_provider_subject,
                email,
                display_name,
                status,
                role,
                personal_title,
                given_name,
                surname,
                birth_date,
                koxo_unique_identifier,
                initials,
                phone,
                is_primary_contact,
                last_login_at,
                created_at,
                updated_at
            ) VALUES (
                @id,
                @customer_id,
                @subject,
                @email,
                @display_name,
                'active',
                @role,
                @personal_title,
                @given_name,
                @surname,
                @birth_date,
                @koxo_unique_identifier,
                @initials,
                @phone,
                0,
                NULL,
                UTC_TIMESTAMP(6),
                UTC_TIMESTAMP(6)
            );
            """;
        userCommand.Parameters.AddWithValue("@id", command.PortalUserId);
        userCommand.Parameters.AddWithValue(
            "@customer_id",
            command.CustomerId);
        userCommand.Parameters.AddWithValue(
            "@subject",
            BillingV2AdditionalUserIdentityConventions.BuildSubject(
                command.PortalUserId));
        userCommand.Parameters.AddWithValue("@email", command.NormalizedEmail);
        userCommand.Parameters.AddWithValue(
            "@display_name",
            command.DisplayName);
        userCommand.Parameters.AddWithValue("@role", PortalRoles.ClientUser);
        userCommand.Parameters.AddWithValue(
            "@personal_title",
            DbValue(command.PersonalTitle));
        userCommand.Parameters.AddWithValue(
            "@given_name",
            DbValue(command.GivenName));
        userCommand.Parameters.AddWithValue(
            "@surname",
            DbValue(command.Surname));
        userCommand.Parameters.AddWithValue(
            "@birth_date",
            command.BirthDate is null
                ? DBNull.Value
                : command.BirthDate.Value.ToString(
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture));
        userCommand.Parameters.AddWithValue(
            "@koxo_unique_identifier",
            koxoUniqueIdentifier);
        userCommand.Parameters.AddWithValue(
            "@initials",
            DbValue(command.Initials));
        userCommand.Parameters.AddWithValue("@phone", DbValue(command.Phone));
        await userCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> AssignSlotAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        BillingV2AdditionalUserAssignmentCommand command,
        CancellationToken cancellationToken)
    {
        await using var slotCommand = connection.CreateCommand();
        slotCommand.Transaction = transaction;
        slotCommand.CommandText =
            """
            UPDATE billing_v2_subscription_users
            SET identity_reference = @identity_reference,
                display_name = @display_name,
                email = @email,
                updated_at = UTC_TIMESTAMP(6)
            WHERE id = @id
              AND subscription_id = @subscription_id
              AND is_primary = 0
              AND identity_reference IS NULL;
            """;
        slotCommand.Parameters.AddWithValue(
            "@identity_reference",
            command.PortalUserId);
        slotCommand.Parameters.AddWithValue(
            "@display_name",
            command.DisplayName);
        slotCommand.Parameters.AddWithValue("@email", command.NormalizedEmail);
        slotCommand.Parameters.AddWithValue("@id", command.SubscriptionUserId);
        slotCommand.Parameters.AddWithValue(
            "@subscription_id",
            command.SubscriptionId);
        return await slotCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertLifecycleAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        BillingV2AdditionalUserAssignmentCommand command,
        string koxoUniqueIdentifier,
        CancellationToken cancellationToken)
    {
        await using var lifecycleCommand = connection.CreateCommand();
        lifecycleCommand.Transaction = transaction;
        lifecycleCommand.CommandText =
            """
            INSERT INTO billing_v2_user_identity_provisioning (
                id,
                subscription_user_id,
                subscription_id,
                customer_id,
                portal_user_id,
                koxo_unique_identifier,
                status,
                created_at,
                updated_at
            ) VALUES (
                @id,
                @subscription_user_id,
                @subscription_id,
                @customer_id,
                @portal_user_id,
                @koxo_unique_identifier,
                @status,
                UTC_TIMESTAMP(6),
                UTC_TIMESTAMP(6)
            );
            """;
        lifecycleCommand.Parameters.AddWithValue("@id", command.LifecycleId);
        lifecycleCommand.Parameters.AddWithValue(
            "@subscription_user_id",
            command.SubscriptionUserId);
        lifecycleCommand.Parameters.AddWithValue(
            "@subscription_id",
            command.SubscriptionId);
        lifecycleCommand.Parameters.AddWithValue(
            "@customer_id",
            command.CustomerId);
        lifecycleCommand.Parameters.AddWithValue(
            "@portal_user_id",
            command.PortalUserId);
        lifecycleCommand.Parameters.AddWithValue(
            "@koxo_unique_identifier",
            koxoUniqueIdentifier);
        lifecycleCommand.Parameters.AddWithValue(
            "@status",
            BillingV2UserIdentityStatuses.AwaitingPassword);
        await lifecycleCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertPasswordSetupAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        BillingV2AdditionalUserAssignmentCommand command,
        CancellationToken cancellationToken)
    {
        await using var setupCommand = connection.CreateCommand();
        setupCommand.Transaction = transaction;
        setupCommand.CommandText =
            """
            INSERT INTO portal_user_password_setups (
                id,
                portal_user_id,
                purpose,
                token_hash,
                expires_at,
                created_at,
                updated_at
            ) VALUES (
                @id,
                @portal_user_id,
                @purpose,
                @token_hash,
                @expires_at,
                UTC_TIMESTAMP(6),
                UTC_TIMESTAMP(6)
            );
            """;
        setupCommand.Parameters.AddWithValue("@id", command.PasswordSetupId);
        setupCommand.Parameters.AddWithValue(
            "@portal_user_id",
            command.PortalUserId);
        setupCommand.Parameters.AddWithValue(
            "@purpose",
            command.PasswordSetupPurpose);
        setupCommand.Parameters.AddWithValue(
            "@token_hash",
            command.PasswordSetupTokenHash);
        setupCommand.Parameters.AddWithValue(
            "@expires_at",
            command.PasswordSetupExpiresAtUtc);
        await setupCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertAuditAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        BillingV2AdditionalUserAssignmentCommand command,
        string koxoUniqueIdentifier,
        CancellationToken cancellationToken)
    {
        await using var auditCommand = connection.CreateCommand();
        auditCommand.Transaction = transaction;
        // Ni l'e-mail ni le jeton : l'audit doit prouver qu'une place a ete
        // attribuee, pas rendre le lien de mot de passe rejouable.
        auditCommand.CommandText =
            """
            INSERT INTO billing_v2_audit_log (
                id,
                entity_type,
                entity_id,
                action,
                actor_reference,
                details_text,
                created_at
            ) VALUES (
                @id,
                'billing_v2_subscription_user',
                @entity_id,
                'billing_v2.additional_user_assigned',
                @actor_reference,
                @details_text,
                UTC_TIMESTAMP(6)
            );
            """;
        auditCommand.Parameters.AddWithValue(
            "@id",
            Guid.NewGuid().ToString("D"));
        auditCommand.Parameters.AddWithValue(
            "@entity_id",
            command.SubscriptionUserId);
        auditCommand.Parameters.AddWithValue(
            "@actor_reference",
            DbValue(command.ActorReference));
        auditCommand.Parameters.AddWithValue(
            "@details_text",
            $"subscription_id={command.SubscriptionId};"
            + $"portal_user_id={command.PortalUserId};"
            + $"koxo_unique_identifier={koxoUniqueIdentifier};"
            + $"lifecycle_status={BillingV2UserIdentityStatuses.AwaitingPassword}");
        await auditCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task<BillingV2AdditionalUserIdentityRecord?> FindByPortalUserIdAsync(
        string portalUserId,
        CancellationToken cancellationToken)
        => FindCoreAsync(
            "lifecycle.portal_user_id = @value",
            portalUserId,
            cancellationToken);

    public Task<BillingV2AdditionalUserIdentityRecord?>
        FindBySubscriptionUserIdAsync(
            string subscriptionUserId,
            CancellationToken cancellationToken)
        => FindCoreAsync(
            "lifecycle.subscription_user_id = @value",
            subscriptionUserId,
            cancellationToken);

    private async Task<BillingV2AdditionalUserIdentityRecord?> FindCoreAsync(
        string predicate,
        string value,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT
                lifecycle.id,
                lifecycle.subscription_user_id,
                lifecycle.subscription_id,
                lifecycle.customer_id,
                lifecycle.portal_user_id,
                lifecycle.koxo_unique_identifier,
                lifecycle.status,
                lifecycle.failure_code,
                lifecycle.directory_object_guid,
                customer.external_reference AS customer_reference,
                portal_user.email,
                portal_user.display_name
            FROM billing_v2_user_identity_provisioning lifecycle
            INNER JOIN customers customer
                ON customer.id = lifecycle.customer_id
            INNER JOIN portal_users portal_user
                ON portal_user.id = lifecycle.portal_user_id
            WHERE {predicate}
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@value", value);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new BillingV2AdditionalUserIdentityRecord(
            MariaDbIdentifierReader.ReadRequired(reader, "id"),
            MariaDbIdentifierReader.ReadRequired(reader, "subscription_user_id"),
            MariaDbIdentifierReader.ReadRequired(reader, "subscription_id"),
            MariaDbIdentifierReader.ReadRequired(reader, "customer_id"),
            reader.GetString("customer_reference"),
            MariaDbIdentifierReader.ReadRequired(reader, "portal_user_id"),
            reader.GetString("koxo_unique_identifier"),
            reader.GetString("status"),
            reader.IsDBNull(reader.GetOrdinal("failure_code"))
                ? null
                : reader.GetString("failure_code"),
            reader.IsDBNull(reader.GetOrdinal("directory_object_guid"))
                ? null
                : reader.GetString("directory_object_guid"),
            reader.GetString("email"),
            reader.GetString("display_name"));
    }

    public async Task<bool> MarkKoxoPendingAsync(
        string id,
        DateTime passwordSetAtUtc,
        DateTime koxoTriggeredAtUtc,
        CancellationToken cancellationToken)
    {
        // `koxo_pending` est aussi accepte en entree : un renvoi de
        // declenchement apres un webhook perdu doit etre un noop reussi, pas un
        // echec. `ready` et `disabled` ne reculent jamais.
        return await ExecuteTransitionAsync(
            """
            UPDATE billing_v2_user_identity_provisioning
            SET status = 'koxo_pending',
                password_set_at = COALESCE(password_set_at, @password_set_at),
                koxo_triggered_at = @koxo_triggered_at,
                failure_code = NULL,
                failure_detail = NULL,
                updated_at = UTC_TIMESTAMP(6)
            WHERE id = @id
              AND status IN ('awaiting_password', 'koxo_pending', 'failed');
            """,
            [
                ("@id", id),
                ("@password_set_at", passwordSetAtUtc),
                ("@koxo_triggered_at", koxoTriggeredAtUtc)
            ],
            cancellationToken);
    }

    public async Task<bool> MarkDirectoryResolvedAsync(
        string id,
        string directoryObjectGuid,
        DateTime resolvedAtUtc,
        CancellationToken cancellationToken)
    {
        // L'egalite NULL-safe interdit de repointer un cycle de vie deja
        // rattache vers un AUTRE objet annuaire : reattribuer une identite
        // donnerait des droits reels au mauvais compte.
        return await ExecuteTransitionAsync(
            """
            UPDATE billing_v2_user_identity_provisioning
            SET status = 'directory_ready',
                directory_object_guid = @object_guid,
                directory_resolved_at = @resolved_at,
                failure_code = NULL,
                failure_detail = NULL,
                updated_at = UTC_TIMESTAMP(6)
            WHERE id = @id
              AND status IN ('koxo_pending', 'directory_ready', 'failed')
              AND (directory_object_guid IS NULL
                   OR directory_object_guid = @object_guid);
            """,
            [
                ("@id", id),
                ("@object_guid", directoryObjectGuid),
                ("@resolved_at", resolvedAtUtc)
            ],
            cancellationToken);
    }

    public async Task<bool> MarkReadyAsync(
        string id,
        DateTime linkedAtUtc,
        CancellationToken cancellationToken)
    {
        return await ExecuteTransitionAsync(
            """
            UPDATE billing_v2_user_identity_provisioning
            SET status = 'ready',
                directory_linked_at = COALESCE(directory_linked_at, @linked_at),
                failure_code = NULL,
                failure_detail = NULL,
                updated_at = UTC_TIMESTAMP(6)
            WHERE id = @id
              AND status IN ('directory_ready', 'ready')
              AND directory_object_guid IS NOT NULL;
            """,
            [("@id", id), ("@linked_at", linkedAtUtc)],
            cancellationToken);
    }

    public async Task<bool> MarkFailedAsync(
        string id,
        string failureCode,
        string? failureDetail,
        CancellationToken cancellationToken)
    {
        return await ExecuteTransitionAsync(
            """
            UPDATE billing_v2_user_identity_provisioning
            SET status = 'failed',
                failure_code = @failure_code,
                failure_detail = @failure_detail,
                updated_at = UTC_TIMESTAMP(6)
            WHERE id = @id
              AND status NOT IN ('ready', 'disabled');
            """,
            [
                ("@id", id),
                ("@failure_code", failureCode),
                ("@failure_detail", (object?)failureDetail ?? DBNull.Value)
            ],
            cancellationToken);
    }

    public async Task<bool> MarkDisabledAsync(
        string id,
        DateTime disabledAtUtc,
        CancellationToken cancellationToken)
    {
        return await ExecuteTransitionAsync(
            """
            UPDATE billing_v2_user_identity_provisioning
            SET status = 'disabled',
                disabled_at = COALESCE(disabled_at, @disabled_at),
                updated_at = UTC_TIMESTAMP(6)
            WHERE id = @id
              AND status <> 'disabled';
            """,
            [("@id", id), ("@disabled_at", disabledAtUtc)],
            cancellationToken);
    }

    private async Task<bool> ExecuteTransitionAsync(
        string sql,
        IReadOnlyList<(string Name, object Value)> parameters,
        CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static async Task<int> ReadCountAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string sql,
        IReadOnlyList<(string Name, object Value)> parameters,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var raw = await command.ExecuteScalarAsync(cancellationToken);
        return raw is null || raw == DBNull.Value
            ? 0
            : Convert.ToInt32(raw, CultureInfo.InvariantCulture);
    }

    private static async Task<string?> ReadNullableScalarAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string sql,
        IReadOnlyList<(string Name, object Value)> parameters,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var raw = await command.ExecuteScalarAsync(cancellationToken);
        return raw is null || raw == DBNull.Value
            ? null
            : Convert.ToString(raw, CultureInfo.InvariantCulture);
    }

    private static object DbValue(string? value)
        => value is null ? DBNull.Value : value;
}
