using System.Text.Json.Serialization;

namespace Microsoft.DevProxy.Plugins.Mocks;

public enum CrudApiActionType
{
    Create,
    GetAll,
    GetOne,
    GetMany,
    Merge,
    Update,
    Delete
}

public enum CrudApiAuthType
{
    None,
    Entra
}

public class CrudApiEntraAuth
{
    public string Audience { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = [];
    public string[] Roles { get; set; } = [];
    public bool ValidateLifetime { get; set; } = false;
    public bool ValidateSigningKey { get; set; } = false;
}

public class CrudApiAction
{
    public CrudApiActionType Action { get; set; } = CrudApiActionType.GetAll;
    public string? Url { get; set; }
    public string? Method { get; set; }
    public string? Query { get; set; }
    public CrudApiAuthType? Auth { get; set; }
    public CrudApiEntraAuth? EntraAuthConfig { get; set; }
}

public class CrudApiConfiguration
{
    public string? ApiFile { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string DataFile { get; set; } = string.Empty;
    public IEnumerable<CrudApiAction> Actions { get; set; } = [];
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CrudApiAuthType Auth { get; set; } = CrudApiAuthType.None;
    public CrudApiEntraAuth? EntraAuthConfig { get; set; }
    [JsonPropertyName("$schema")]
    public string Schema { get; set; } = "https://raw.githubusercontent.com/microsoft/dev-proxy/main/schemas/v0.23.0/crudapiplugin.schema.json";
}