using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EzDocument;

/// <summary>Finds MVC / Web API controllers and their attribute-routed (or conventional) actions.</summary>
internal sealed class ControllerScanner
{
    private static readonly Dictionary<string, string> HttpMethods = new(StringComparer.Ordinal)
    {
        ["HttpGet"] = "GET",
        ["HttpPost"] = "POST",
        ["HttpPut"] = "PUT",
        ["HttpPatch"] = "PATCH",
        ["HttpDelete"] = "DELETE",
        ["HttpHead"] = "HEAD",
        ["HttpOptions"] = "OPTIONS"
    };

    private readonly SourceIndex _index;
    private readonly ParameterClassifier _classifier;

    public ControllerScanner(SourceIndex index)
    {
        _index = index;
        _classifier = new ParameterClassifier(index);
    }

    public IEnumerable<EndpointGroup> Scan(SourceFile file)
    {
        foreach (var declaration in file.Root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            if (!IsController(declaration))
            {
                continue;
            }

            var controllerName = declaration.Identifier.ValueText;
            var controllerToken = controllerName.EndsWith("Controller", StringComparison.Ordinal) && controllerName.Length > "Controller".Length
                ? controllerName[..^"Controller".Length]
                : controllerName;
            var isApiController = _index.HasAssemblyApiController || HasAttributeInHierarchy(declaration, "ApiController");
            var area = SourceIndex.FindAttribute(declaration.AttributeLists, "Area") is { } areaAttribute
                ? RouteArgument(areaAttribute)
                : null;
            var controllerRoutes = AttributesNamed(declaration.AttributeLists, "Route").Select(RouteArgument).ToList();
            var controllerAuthorized = SourceIndex.HasAttribute(declaration.AttributeLists, "Authorize");

            var endpoints = declaration.Members
                .OfType<MethodDeclarationSyntax>()
                .Where(IsAction)
                .SelectMany(method => GetEndpoints(method, controllerRoutes, controllerToken, area, isApiController, controllerAuthorized))
                .ToList();

            yield return new EndpointGroup(controllerName, EndpointGroupKind.Controller, file.RelativePath, SourceIndex.Summary(declaration), endpoints);
        }
    }

    private bool IsController(ClassDeclarationSyntax declaration)
    {
        if (declaration.Modifiers.Any(SyntaxKind.AbstractKeyword) || declaration.Modifiers.Any(SyntaxKind.StaticKeyword)
            || SourceIndex.HasAttribute(declaration.AttributeLists, "NonController"))
        {
            return false;
        }

        return declaration.Identifier.ValueText.EndsWith("Controller", StringComparison.Ordinal)
               || SourceIndex.HasAttribute(declaration.AttributeLists, "ApiController")
               || SourceIndex.HasAttribute(declaration.AttributeLists, "Controller")
               || declaration.BaseList?.Types.Any(baseType => SourceIndex.Unwrap(baseType.Type).Name is "Controller" or "ControllerBase") == true;
    }

    private static bool HasAttributeInHierarchy(ClassDeclarationSyntax declaration, string attribute) =>
        SourceIndex.HasAttribute(declaration.AttributeLists, attribute);

    private static bool IsAction(MethodDeclarationSyntax method) =>
        method.Modifiers.Any(SyntaxKind.PublicKeyword)
        && !method.Modifiers.Any(SyntaxKind.StaticKeyword)
        && !method.Modifiers.Any(SyntaxKind.OverrideKeyword)
        && !SourceIndex.HasAttribute(method.AttributeLists, "NonAction");

    private IEnumerable<EndpointInfo> GetEndpoints(
        MethodDeclarationSyntax method,
        IReadOnlyList<string?> controllerRoutes,
        string controllerToken,
        string? area,
        bool isApiController,
        bool controllerAuthorized)
    {
        var attributes = method.AttributeLists.SelectMany(list => list.Attributes).ToList();
        var verbAttributes = attributes.Where(a => HttpMethods.ContainsKey(SourceIndex.AttributeName(a))).ToList();
        var actionRouteAttributes = attributes.Where(a => SourceIndex.AttributeName(a) == "Route").ToList();
        var actionName = SourceIndex.FindAttribute(method.AttributeLists, "ActionName") is { } actionNameAttribute
            ? RouteArgument(actionNameAttribute) ?? method.Identifier.ValueText
            : method.Identifier.ValueText;
        var conventional = false;

        if (verbAttributes.Count == 0 && actionRouteAttributes.Count == 0)
        {
            // Attribute-routed controllers only expose attributed actions. Plain MVC controllers fall back
            // to the default conventional route: {controller=Home}/{action=Index}/{id?}.
            if (isApiController || controllerRoutes.Count > 0)
            {
                yield break;
            }

            conventional = true;
        }

        var methodRoutes = verbAttributes.Count > 0
            ? verbAttributes.Select(attribute => (HttpMethods[SourceIndex.AttributeName(attribute)], RouteArgument(attribute))).ToList()
            : new List<(string, string?)> { ("ANY", null) };

        var authorized = (controllerAuthorized || SourceIndex.HasAttribute(method.AttributeLists, "Authorize"))
                         && !SourceIndex.HasAttribute(method.AttributeLists, "AllowAnonymous");
        var (responseType, responses) = Responses(method);
        var routeControllers = controllerRoutes.Count == 0 ? new string?[] { null } : controllerRoutes.ToArray();

        foreach (var (httpMethod, verbRoute) in methodRoutes)
        {
            var actionRoutes = verbRoute is not null
                ? new string?[] { verbRoute }
                : actionRouteAttributes.Count > 0
                    ? actionRouteAttributes.Select(RouteArgument).ToArray()
                    : new string?[] { null };

            foreach (var controllerRoute in routeControllers)
            foreach (var actionRoute in actionRoutes)
            {
                string route;
                if (conventional)
                {
                    var hasId = method.ParameterList.Parameters.Any(p => p.Identifier.ValueText.Equals("id", StringComparison.OrdinalIgnoreCase));
                    route = $"/{controllerToken}/{actionName}" + (hasId ? "/{id?}" : string.Empty);
                }
                else
                {
                    var combined = ReplaceRouteTokens(CombineRoutes(controllerRoute, actionRoute), controllerToken, actionName, area);
                    route = "/" + (combined ?? string.Empty).Trim('/');
                }

                var routeParameters = ParameterClassifier.RouteParameters(route);
                var parameters = method.ParameterList.Parameters
                    .SelectMany(parameter => _classifier.Classify(
                        parameter,
                        httpMethod,
                        routeParameters,
                        inferBody: isApiController,
                        isMinimalApi: false,
                        SourceIndex.ParameterSummary(method, parameter.Identifier.ValueText)))
                    .ToList();

                yield return new EndpointInfo(
                    httpMethod,
                    route,
                    method.Identifier.ValueText,
                    SourceIndex.Summary(method),
                    parameters,
                    responseType,
                    responses,
                    authorized,
                    conventional);
            }
        }
    }

    private (string? ResponseType, IReadOnlyList<ResponseInfo> Responses) Responses(MethodDeclarationSyntax method)
    {
        var responses = new List<ResponseInfo>();
        foreach (var attribute in method.AttributeLists.SelectMany(list => list.Attributes))
        {
            if (SourceIndex.AttributeName(attribute) != "ProducesResponseType")
            {
                continue;
            }

            string? type = attribute.Name is GenericNameSyntax generic ? generic.TypeArgumentList.Arguments[0].ToString()
                : attribute.Name is QualifiedNameSyntax { Right: GenericNameSyntax qualifiedGeneric } ? qualifiedGeneric.TypeArgumentList.Arguments[0].ToString()
                : null;
            string? status = null;
            foreach (var argument in attribute.ArgumentList?.Arguments ?? default)
            {
                switch (argument.Expression)
                {
                    case TypeOfExpressionSyntax typeOf:
                        type = typeOf.Type.ToString();
                        break;
                    case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.NumericLiteralExpression):
                        status = literal.Token.ValueText;
                        break;
                    case MemberAccessExpressionSyntax member when Regex.Match(member.Name.Identifier.ValueText, @"\d{3}") is { Success: true } match:
                        status = match.Value;
                        break;
                }
            }

            responses.Add(new ResponseInfo(status ?? "200", type));
        }

        return (UnwrapResponseType(method.ReturnType), responses);
    }

    /// <summary>Task&lt;ActionResult&lt;T&gt;&gt; → T; IActionResult and void yield null.</summary>
    internal static string? UnwrapResponseType(TypeSyntax type)
    {
        while (type is GenericNameSyntax generic && generic.TypeArgumentList.Arguments.Count == 1
               && generic.Identifier.ValueText is "Task" or "ValueTask" or "ActionResult" or "Ok" or "Results")
        {
            type = generic.TypeArgumentList.Arguments[0];
        }

        var text = type.ToString();
        return text is "void" or "Task" or "ValueTask" or "IActionResult" or "ActionResult" or "IResult" or "Task<IActionResult>"
            ? null
            : text;
    }

    private string? RouteArgument(AttributeSyntax attribute)
    {
        if (attribute.ArgumentList is not { } argumentList)
        {
            return null;
        }

        var arguments = argumentList.Arguments;
        var template = arguments.FirstOrDefault(argument =>
                           argument.NameEquals?.Name.Identifier.ValueText.Equals("Template", StringComparison.OrdinalIgnoreCase) == true
                           || argument.NameColon?.Name.Identifier.ValueText.Equals("template", StringComparison.OrdinalIgnoreCase) == true)
                       ?? arguments.FirstOrDefault(argument => argument.NameEquals is null && argument.NameColon is null);
        if (template is null)
        {
            return null;
        }

        return _index.EvaluateString(template.Expression) ?? $"{{unresolved: {template.Expression}}}";
    }

    private static IEnumerable<AttributeSyntax> AttributesNamed(SyntaxList<AttributeListSyntax> lists, string name) =>
        lists.SelectMany(list => list.Attributes).Where(attribute => SourceIndex.AttributeName(attribute) == name);

    internal static string? CombineRoutes(string? controllerRoute, string? actionRoute)
    {
        if (string.IsNullOrWhiteSpace(actionRoute))
        {
            return controllerRoute;
        }

        if (actionRoute.StartsWith("~/", StringComparison.Ordinal))
        {
            return actionRoute[2..];
        }

        if (actionRoute.StartsWith('/'))
        {
            return actionRoute;
        }

        return string.Join("/", new[] { controllerRoute, actionRoute }
            .Where(route => !string.IsNullOrWhiteSpace(route))
            .Select(route => route!.Trim('/')));
    }

    private static string? ReplaceRouteTokens(string? route, string controller, string action, string? area) =>
        route is null
            ? null
            : Regex.Replace(route, @"\[(controller|action|area)\]", match => match.Groups[1].Value.ToLowerInvariant() switch
            {
                "controller" => controller,
                "action" => action,
                "area" => area ?? match.Value,
                _ => match.Value
            }, RegexOptions.IgnoreCase);
}
