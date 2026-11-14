namespace EzDocument;

public enum EndpointGroupKind
{
    Controller,
    MinimalApi
}

public enum ParameterSource
{
    Route,
    Query,
    Header,
    Body,
    Form
}

public sealed record EndpointGroup(
    string Name,
    EndpointGroupKind Kind,
    string SourcePath,
    string? Summary,
    IReadOnlyList<EndpointInfo> Endpoints);

public sealed record EndpointInfo(
    string Method,
    string Route,
    string Handler,
    string? Summary,
    IReadOnlyList<EndpointParameter> Parameters,
    string? ResponseType,
    IReadOnlyList<ResponseInfo> Responses,
    bool RequiresAuthorization,
    bool IsConventionalRoute = false)
{
    public EndpointParameter? Body => Parameters.FirstOrDefault(parameter => parameter.Source == ParameterSource.Body);

    public bool HasForm => Parameters.Any(parameter => parameter.Source == ParameterSource.Form);

    public bool RequiresBody => Body is { Required: true } || Parameters.Any(p => p.Source == ParameterSource.Form && p.Required);

    public bool AcceptsBody => Body is not null || HasForm;

    public IEnumerable<EndpointParameter> ParametersFrom(ParameterSource source) =>
        Parameters.Where(parameter => parameter.Source == source);

    public bool HasParameters => Parameters.Any(parameter =>
        parameter.Source is ParameterSource.Route or ParameterSource.Query or ParameterSource.Header);
}

public sealed record EndpointParameter(
    string Name,
    string Type,
    ParameterSource Source,
    bool Required,
    string? Summary,
    TypeShape? Shape = null,
    IReadOnlyList<string>? EnumValues = null);

public sealed record ResponseInfo(string StatusCode, string? Type);

/// <summary>Resolved shape of a request DTO found in the scanned source.</summary>
public sealed record TypeShape(string Name, bool IsCollection, IReadOnlyList<PropertyShape> Properties);

public sealed record PropertyShape(
    string JsonName,
    string Type,
    bool Required,
    string? Summary,
    TypeShape? Nested,
    IReadOnlyList<string>? EnumValues = null);
