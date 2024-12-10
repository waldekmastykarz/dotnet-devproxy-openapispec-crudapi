using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.DevProxy.Plugins.Mocks;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;

var jsonSerializerOptions = new JsonSerializerOptions
{
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault | JsonIgnoreCondition.WhenWritingNull,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true
};
jsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

(OpenApiDocument document, OpenApiDiagnostic diagnostic) LoadApiSpec(string path)
{
    var doc = new OpenApiStringReader().Read(File.ReadAllText(path), out OpenApiDiagnostic diagnostic);
    return (doc, diagnostic);
}

CrudApiConfiguration InitCrudApiConfig(string baseUrl, string resourceName)
{
    return new CrudApiConfiguration
    {
        BaseUrl = baseUrl,
        DataFile = $"{resourceName}-data.json",
        Actions = new List<CrudApiAction>()
    };
}

CrudApiActionType GetActionType(OperationType operationType, OpenApiOperation operation)
{
    // for non-GET operations, we can simply map the method
    if (operationType != OperationType.Get)
    {
        return operationType switch
        {
            OperationType.Post => CrudApiActionType.Create,
            OperationType.Patch => CrudApiActionType.Merge,
            OperationType.Put => CrudApiActionType.Update,
            OperationType.Delete => CrudApiActionType.Delete,
            _ => throw new NotSupportedException($"{operationType} is not a supported operation type")
        };
    }

    // for GET operations we need to figure out from the response shape
    // if the operation returns a single item, or a list of items

    // no parameters means that we're getting all items
    if (!operation.Parameters.Where(p => p.In == ParameterLocation.Path).Any())
    {
        return CrudApiActionType.GetAll;
    }

    var okResponse = operation.Responses.FirstOrDefault(r => r.Key == "2XX").Value ??
        throw new NotSupportedException($"Couldn't find successful response for operation {operation.Summary}");

    var schema = okResponse.Content.First().Value.Schema;
    var rootObjectType = schema.Type;
    if (rootObjectType == "array")
    {
        return CrudApiActionType.GetMany;
    }
    if (rootObjectType == "object")
    {
        if (schema.Properties.TryGetValue("value", out OpenApiSchema? value))
        {
            return value.Type == "array" ? CrudApiActionType.GetMany : CrudApiActionType.GetOne;
        }
        else
        {
            return CrudApiActionType.GetOne;
        }
    }

    return CrudApiActionType.GetOne;
}

void LoadParametersInPath(OpenApiOperation operation, CrudApiAction apiAction)
{
    var parametersInPath = operation.Parameters.Where(p => p.In == ParameterLocation.Path);
    if (!parametersInPath.Any())
    {
        return;
    }

    var queryParams = new List<string>();
    foreach (var parameter in parametersInPath)
    {
        switch (parameter.Schema.Type)
        {
            case "string":
                queryParams.Add($"@.id == '{{{parameter.Name}}}'");
                break;
            case "integer":
            case "float":
                queryParams.Add($"@.id == {{{parameter.Name}}}");
                break;
            default:
                break;
        }
    }

    var query = $"$.[?({string.Join(" && ", queryParams)})]";
    apiAction.Query = query;
}

void LoadActions(OpenApiDocument apiDoc, CrudApiConfiguration apiConfiguration)
{
    foreach (var path in apiDoc.Paths)
    {
        foreach (var operation in path.Value.Operations)
        {
            var action = new CrudApiAction
            {
                Action = GetActionType(operation.Key, operation.Value),
                Url = path.Key
            };
            LoadParametersInPath(operation.Value, action);
            (apiConfiguration.Actions as List<CrudApiAction>)?.Add(action);
        }
    }
}

void SaveApiDefinition(CrudApiConfiguration apiConfiguration, string resourceName)
{
    File.WriteAllText($"{resourceName}-api.json", JsonSerializer.Serialize(apiConfiguration, jsonSerializerOptions));
}

void Process()
{
    var resourceName = "users";
    var apiSpecFileName = "Users.yml";

    var (apiDoc, _) = LoadApiSpec(apiSpecFileName);
    var apiConfiguration = InitCrudApiConfig(apiDoc.Servers.First().Url, resourceName);
    LoadActions(apiDoc, apiConfiguration);
    SaveApiDefinition(apiConfiguration, resourceName);
}

Process();