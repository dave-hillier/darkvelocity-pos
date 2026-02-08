using Authzed.Api.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;

namespace DarkVelocity.E2E.Auth;

public sealed class SpiceDbTestHelper
{
    private const string Endpoint = "http://localhost:50051";
    private const string PresharedKey = "darkvelocity_dev_key";

    private readonly GrpcChannel _channel;
    private readonly SchemaService.SchemaServiceClient _schemaClient;
    private readonly PermissionsService.PermissionsServiceClient _permissionsClient;
    private readonly Metadata _metadata;

    public SpiceDbTestHelper()
    {
        _channel = GrpcChannel.ForAddress(Endpoint, new GrpcChannelOptions
        {
            Credentials = ChannelCredentials.Insecure
        });

        _schemaClient = new SchemaService.SchemaServiceClient(_channel);
        _permissionsClient = new PermissionsService.PermissionsServiceClient(_channel);
        _metadata = new Metadata
        {
            { "authorization", $"Bearer {PresharedKey}" }
        };
    }

    public async Task WriteSchemaAsync()
    {
        var schemaPath = FindSchemaFile();
        var schemaText = await File.ReadAllTextAsync(schemaPath);

        await _schemaClient.WriteSchemaAsync(
            new WriteSchemaRequest { Schema = schemaText },
            headers: _metadata);
    }

    public async Task SetupUserForSiteOperationsAsync(Guid userId, Guid orgId, Guid siteId)
    {
        var siteResourceId = $"{orgId}/{siteId}";
        var userIdStr = userId.ToString();

        var updates = new[]
        {
            // site -> organization
            MakeUpdate("site", siteResourceId, "organization", "organization", orgId.ToString()),
            // site -> staff
            MakeUpdate("site", siteResourceId, "staff", "user", userIdStr),
            // site -> active_session (with session_scope caveat, scope="pos")
            MakeUpdate("site", siteResourceId, "active_session", "user", userIdStr,
                caveatName: "session_scope",
                caveatContext: new Dictionary<string, object> { ["scope"] = "pos" }),
        };

        var request = new WriteRelationshipsRequest();
        request.Updates.AddRange(updates);

        await _permissionsClient.WriteRelationshipsAsync(
            request,
            headers: _metadata);
    }

    private static RelationshipUpdate MakeUpdate(
        string resourceType,
        string resourceId,
        string relation,
        string subjectType,
        string subjectId,
        string? caveatName = null,
        Dictionary<string, object>? caveatContext = null)
    {
        var relationship = new Relationship
        {
            Resource = new ObjectReference
            {
                ObjectType = resourceType,
                ObjectId = resourceId
            },
            Relation = relation,
            Subject = new SubjectReference
            {
                Object = new ObjectReference
                {
                    ObjectType = subjectType,
                    ObjectId = subjectId
                }
            }
        };

        if (caveatName is not null)
        {
            relationship.OptionalCaveat = new ContextualizedCaveat
            {
                CaveatName = caveatName
            };

            if (caveatContext is not null)
            {
                var contextStruct = new Struct();
                foreach (var (key, value) in caveatContext)
                {
                    contextStruct.Fields[key] = value switch
                    {
                        string s => Value.ForString(s),
                        bool b => Value.ForBool(b),
                        _ => Value.ForString(value.ToString()!)
                    };
                }
                relationship.OptionalCaveat.Context = contextStruct;
            }
        }

        return new RelationshipUpdate
        {
            Operation = RelationshipUpdate.Types.Operation.Touch,
            Relationship = relationship
        };
    }

    private static string FindSchemaFile()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "docker", "spicedb", "schema.zed");
            if (File.Exists(candidate))
                return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new FileNotFoundException(
            "Could not find docker/spicedb/schema.zed. Run tests from the solution directory.");
    }
}
