using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EzDocument;

/// <summary>
/// Decides where a handler parameter is bound from, following ASP.NET Core's binding rules:
/// explicit [From*] attributes first, then route-template names, special framework types,
/// simple types (query) and finally complex types (request body).
/// </summary>
internal sealed class ParameterClassifier
{
    private static readonly Regex RouteParameterPattern = new(@"(?<!\{)\{\*{0,2}(?<name>[A-Za-z_][A-Za-z0-9_]*)(?<rest>[^{}]*)\}", RegexOptions.Compiled);

    // Framework-provided values that never come from the HTTP request.
    private static readonly HashSet<string> IgnoredTypes = new(StringComparer.Ordinal)
    {
        "CancellationToken", "HttpContext", "HttpRequest", "HttpResponse", "ClaimsPrincipal",
        "ILogger", "ILoggerFactory", "IServiceProvider", "LinkGenerator"
    };

    private static readonly HashSet<string> FormTypes = new(StringComparer.Ordinal)
    {
        "IFormFile", "IFormFileCollection", "IFormCollection"
    };

    private static readonly HashSet<string> RawBodyTypes = new(StringComparer.Ordinal) { "Stream", "PipeReader" };

    private static readonly HashSet<string> BodylessMethods = new(StringComparer.Ordinal) { "GET", "HEAD", "DELETE", "OPTIONS" };

    private readonly SourceIndex _index;

    public ParameterClassifier(SourceIndex index) => _index = index;

    public static IReadOnlyDictionary<string, bool> RouteParameters(string route)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in RouteParameterPattern.Matches(route))
        {
            var rest = match.Groups["rest"].Value;
            var optional = rest.EndsWith('?') || rest.Contains('=');
            result[match.Groups["name"].Value] = !optional;
        }

        return result;
    }

    /// <param name="inferBody">True for [ApiController] controllers and minimal APIs, which infer [FromBody] for complex types.</param>
    public IEnumerable<EndpointParameter> Classify(
        ParameterSyntax parameter,
        string httpMethod,
        IReadOnlyDictionary<string, bool> routeParameters,
        bool inferBody,
        bool isMinimalApi,
        string? summary)
    {
        var name = parameter.Identifier.ValueText;
        var typeText = parameter.Type?.ToString() ?? "unknown";
        var (typeName, _) = SourceIndex.Unwrap(parameter.Type);
        var attributes = parameter.AttributeLists;
        var isNullable = parameter.Type is NullableTypeSyntax;
        var hasDefault = parameter.Default is not null;
        var explicitRequired = SourceIndex.HasAttribute(attributes, "Required") || SourceIndex.HasAttribute(attributes, "BindRequired");
        var optional = !explicitRequired && (isNullable || hasDefault);

        if (SourceIndex.HasAttribute(attributes, "FromServices") || SourceIndex.HasAttribute(attributes, "FromKeyedServices")
            || typeName is not null && IgnoredTypes.Contains(typeName))
        {
            yield break;
        }

        // [AsParameters] spreads a type's properties into individual bindings (minimal APIs).
        if (SourceIndex.HasAttribute(attributes, "AsParameters"))
        {
            var shape = _index.GetShape(parameter.Type);
            if (shape is null)
            {
                yield return new EndpointParameter(name, typeText, ParameterSource.Query, !optional, summary);
                yield break;
            }

            foreach (var property in shape.Properties)
            {
                var spreadSource = routeParameters.ContainsKey(property.JsonName) ? ParameterSource.Route
                    : property.Nested is not null ? ParameterSource.Body
                    : ParameterSource.Query;
                yield return new EndpointParameter(property.JsonName, property.Type, spreadSource, property.Required, property.Summary, property.Nested, property.EnumValues);
            }

            yield break;
        }

        var explicitSource = ExplicitSource(attributes, out var bindingName);
        if (explicitSource is not null)
        {
            var bound = bindingName ?? name;
            var required = explicitSource switch
            {
                ParameterSource.Route => !routeParameters.TryGetValue(bound, out var routeRequired) || routeRequired,
                _ => !optional
            };
            var shape = explicitSource is ParameterSource.Body or ParameterSource.Form ? _index.GetShape(parameter.Type) : null;
            yield return new EndpointParameter(bound, typeText, explicitSource.Value, required, summary, shape, _index.EnumValues(parameter.Type));
            yield break;
        }

        if (routeParameters.TryGetValue(name, out var isRequiredRouteValue))
        {
            yield return new EndpointParameter(name, typeText, ParameterSource.Route, isRequiredRouteValue, summary, EnumValues: _index.EnumValues(parameter.Type));
            yield break;
        }

        if (typeName is not null && FormTypes.Contains(typeName))
        {
            yield return new EndpointParameter(name, typeText, ParameterSource.Form, !optional, summary);
            yield break;
        }

        if (typeName is not null && RawBodyTypes.Contains(typeName))
        {
            yield return new EndpointParameter(name, typeText, ParameterSource.Body, !optional, summary);
            yield break;
        }

        if (_index.IsSimpleType(parameter.Type))
        {
            yield return new EndpointParameter(name, typeText, ParameterSource.Query, !optional, summary, EnumValues: _index.EnumValues(parameter.Type));
            yield break;
        }

        // Minimal APIs resolve interface-typed parameters from DI; treat them as services.
        if (isMinimalApi && typeName is { Length: > 1 } && typeName[0] == 'I' && char.IsUpper(typeName[1]))
        {
            yield break;
        }

        if (inferBody && !(isMinimalApi && BodylessMethods.Contains(httpMethod)))
        {
            yield return new EndpointParameter(name, typeText, ParameterSource.Body, !optional, summary, _index.GetShape(parameter.Type));
            yield break;
        }

        // Without [ApiController], MVC model-binds complex types from form fields, route values and query string.
        var source = BodylessMethods.Contains(httpMethod) ? ParameterSource.Query : ParameterSource.Form;
        yield return new EndpointParameter(name, typeText, source, !optional, summary, _index.GetShape(parameter.Type));
    }

    private static ParameterSource? ExplicitSource(Microsoft.CodeAnalysis.SyntaxList<AttributeListSyntax> lists, out string? bindingName)
    {
        bindingName = null;
        foreach (var attribute in lists.SelectMany(list => list.Attributes))
        {
            ParameterSource? source = SourceIndex.AttributeName(attribute) switch
            {
                "FromBody" => ParameterSource.Body,
                "FromForm" => ParameterSource.Form,
                "FromQuery" => ParameterSource.Query,
                "FromRoute" => ParameterSource.Route,
                "FromHeader" => ParameterSource.Header,
                _ => null
            };
            if (source is null)
            {
                continue;
            }

            bindingName = attribute.ArgumentList?.Arguments
                .Where(argument => argument.NameEquals?.Name.Identifier.ValueText == "Name")
                .Select(argument => (argument.Expression as LiteralExpressionSyntax)?.Token.ValueText)
                .FirstOrDefault();
            return source;
        }

        return null;
    }
}
