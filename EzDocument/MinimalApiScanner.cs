using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EzDocument;

/// <summary>Finds minimal API registrations such as <c>app.MapGet("/x", handler)</c>, including MapGroup prefixes.</summary>
internal sealed class MinimalApiScanner
{
    private static readonly Dictionary<string, string> MapMethods = new(StringComparer.Ordinal)
    {
        ["MapGet"] = "GET",
        ["MapPost"] = "POST",
        ["MapPut"] = "PUT",
        ["MapPatch"] = "PATCH",
        ["MapDelete"] = "DELETE"
    };

    private readonly SourceIndex _index;
    private readonly ParameterClassifier _classifier;

    public MinimalApiScanner(SourceIndex index)
    {
        _index = index;
        _classifier = new ParameterClassifier(index);
    }

    public EndpointGroup? Scan(SourceFile file)
    {
        var endpoints = new List<EndpointInfo>();
        foreach (var invocation in file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax member
                || !MapMethods.TryGetValue(member.Name.Identifier.ValueText, out var httpMethod)
                || invocation.ArgumentList.Arguments.Count < 2)
            {
                continue;
            }

            var routeExpression = invocation.ArgumentList.Arguments[0].Expression;
            var pattern = _index.EvaluateString(routeExpression);
            if (pattern is null && routeExpression is not LiteralExpressionSyntax)
            {
                // Not a route string (e.g. an unrelated MapGet extension) - skip rather than guess.
                if (routeExpression is not (IdentifierNameSyntax or MemberAccessExpressionSyntax or InterpolatedStringExpressionSyntax))
                {
                    continue;
                }

                pattern = $"{{unresolved: {routeExpression}}}";
            }

            var groupAuthorized = false;
            var prefix = ResolvePrefix(member.Expression, invocation, ref groupAuthorized, 0);
            var route = "/" + string.Join("/", new[] { prefix, pattern }
                .Where(segment => !string.IsNullOrWhiteSpace(segment))
                .Select(segment => segment!.Trim('/')))
                .Trim('/');

            var chain = FluentChain(invocation).ToList();
            var authorized = (groupAuthorized || chain.Contains("RequireAuthorization"))
                             && !chain.Contains("AllowAnonymous");

            var handlerExpression = invocation.ArgumentList.Arguments[^1].Expression;
            var (parameters, handlerName, summaryNode, responseType, handlerAttributes) = DescribeHandler(handlerExpression, file);
            if (handlerAttributes.HasValue)
            {
                authorized = (authorized || SourceIndex.HasAttribute(handlerAttributes.Value, "Authorize"))
                             && !SourceIndex.HasAttribute(handlerAttributes.Value, "AllowAnonymous");
            }

            var routeParameters = ParameterClassifier.RouteParameters(route);
            var classified = parameters
                .SelectMany(parameter => _classifier.Classify(
                    parameter,
                    httpMethod,
                    routeParameters,
                    inferBody: true,
                    isMinimalApi: true,
                    summaryNode is null ? null : SourceIndex.ParameterSummary(summaryNode, parameter.Identifier.ValueText)))
                .ToList();

            var summary = ChainArgument(invocation, "WithSummary") ?? ChainArgument(invocation, "WithDescription")
                          ?? (summaryNode is null ? null : SourceIndex.Summary(summaryNode));

            endpoints.Add(new EndpointInfo(
                httpMethod,
                route,
                handlerName,
                summary,
                classified,
                responseType,
                Array.Empty<ResponseInfo>(),
                authorized));
        }

        return endpoints.Count == 0
            ? null
            : new EndpointGroup(Path.GetFileName(file.RelativePath), EndpointGroupKind.MinimalApi, file.RelativePath, null, endpoints);
    }

    private (IReadOnlyList<ParameterSyntax> Parameters, string Name, SyntaxNode? SummaryNode, string? ResponseType, SyntaxList<AttributeListSyntax>? Attributes)
        DescribeHandler(ExpressionSyntax handler, SourceFile file)
    {
        switch (handler)
        {
            case ParenthesizedLambdaExpressionSyntax lambda:
                return (lambda.ParameterList.Parameters.ToList(), "(lambda)", null,
                    lambda.ReturnType is null ? null : ControllerScanner.UnwrapResponseType(lambda.ReturnType), lambda.AttributeLists);
            case SimpleLambdaExpressionSyntax simple:
                return (new[] { simple.Parameter }, "(lambda)", null, null, simple.AttributeLists);
            case AnonymousMethodExpressionSyntax anonymous:
                return (anonymous.ParameterList?.Parameters.ToList() ?? new List<ParameterSyntax>(), "(delegate)", null, null, null);
            case IdentifierNameSyntax or MemberAccessExpressionSyntax:
                var name = handler is MemberAccessExpressionSyntax access ? access.Name.Identifier.ValueText : ((IdentifierNameSyntax)handler).Identifier.ValueText;
                var method = _index.FindMethod(name, file.Root.SyntaxTree);
                return method is null
                    ? (Array.Empty<ParameterSyntax>(), handler.ToString(), null, null, null)
                    : (method.ParameterList.Parameters.ToList(), handler.ToString(), method, ControllerScanner.UnwrapResponseType(method.ReturnType), method.AttributeLists);
            default:
                return (Array.Empty<ParameterSyntax>(), handler.ToString(), null, null, null);
        }
    }

    /// <summary>Walks a route-builder receiver back through MapGroup calls and local variables to build the prefix.</summary>
    private string? ResolvePrefix(ExpressionSyntax receiver, SyntaxNode usage, ref bool authorized, int depth)
    {
        if (depth > 10)
        {
            return null;
        }

        switch (receiver)
        {
            case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member } invocation:
                var parent = ResolvePrefix(member.Expression, usage, ref authorized, depth + 1);
                var methodName = member.Name.Identifier.ValueText;
                if (methodName == "RequireAuthorization")
                {
                    authorized = true;
                }

                if (methodName == "MapGroup" && invocation.ArgumentList.Arguments.Count > 0)
                {
                    var segment = _index.EvaluateString(invocation.ArgumentList.Arguments[0].Expression) ?? "{unresolved}";
                    return string.Join("/", new[] { parent, segment }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim('/')));
                }

                return parent;
            case IdentifierNameSyntax identifier:
                // Find the nearest preceding declaration of this variable in the same file.
                var declarator = usage.SyntaxTree.GetRoot()
                    .DescendantNodes()
                    .OfType<VariableDeclaratorSyntax>()
                    .Where(v => v.Identifier.ValueText == identifier.Identifier.ValueText
                                && v.Initializer is not null
                                && v.SpanStart < usage.SpanStart)
                    .LastOrDefault();
                return declarator is null ? null : ResolvePrefix(declarator.Initializer!.Value, declarator, ref authorized, depth + 1);
            default:
                return null;
        }
    }

    /// <summary>Names of fluent calls chained after the Map* call, e.g. .RequireAuthorization().WithName("x").</summary>
    private static IEnumerable<string> FluentChain(InvocationExpressionSyntax invocation)
    {
        SyntaxNode current = invocation;
        while (current.Parent is MemberAccessExpressionSyntax member && member.Parent is InvocationExpressionSyntax next)
        {
            yield return member.Name.Identifier.ValueText;
            current = next;
        }
    }

    private string? ChainArgument(InvocationExpressionSyntax invocation, string methodName)
    {
        SyntaxNode current = invocation;
        while (current.Parent is MemberAccessExpressionSyntax member && member.Parent is InvocationExpressionSyntax next)
        {
            if (member.Name.Identifier.ValueText == methodName && next.ArgumentList.Arguments.Count > 0)
            {
                return _index.EvaluateString(next.ArgumentList.Arguments[0].Expression);
            }

            current = next;
        }

        return null;
    }
}
