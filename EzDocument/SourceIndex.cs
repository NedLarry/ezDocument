using System.Net;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EzDocument;

/// <summary>
/// Syntax-only index over every scanned source file. It lets the scanners resolve
/// constant route strings, enums and request DTO shapes without compiling the project.
/// </summary>
internal sealed class SourceIndex
{
    private const int MaxDepth = 8;

    private static readonly HashSet<string> SimpleTypes = new(StringComparer.Ordinal)
    {
        "string", "String", "char", "Char", "bool", "Boolean",
        "byte", "Byte", "sbyte", "SByte", "short", "Int16", "ushort", "UInt16",
        "int", "Int32", "uint", "UInt32", "long", "Int64", "ulong", "UInt64",
        "nint", "nuint", "float", "Single", "double", "Double", "decimal", "Decimal",
        "Guid", "DateTime", "DateTimeOffset", "DateOnly", "TimeOnly", "TimeSpan", "Uri", "Version"
    };

    private static readonly HashSet<string> CollectionTypes = new(StringComparer.Ordinal)
    {
        "List", "IList", "IEnumerable", "ICollection", "IReadOnlyList", "IReadOnlyCollection",
        "Collection", "HashSet", "ISet", "IReadOnlySet", "ImmutableArray", "ImmutableList"
    };

    private readonly Dictionary<string, List<TypeDeclarationSyntax>> _types = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<string>> _enums = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ExpressionSyntax> _constants = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<MethodDeclarationSyntax>> _methods = new(StringComparer.Ordinal);

    public SourceIndex(IEnumerable<SourceFile> files)
    {
        Files = files.ToList();
        foreach (var file in Files)
        {
            var root = file.Root;
            HasAssemblyApiController |= root.AttributeLists
                .Where(list => list.Target?.Identifier.IsKind(SyntaxKind.AssemblyKeyword) == true)
                .SelectMany(list => list.Attributes)
                .Any(attribute => AttributeName(attribute) == "ApiController");

            foreach (var node in root.DescendantNodes())
            {
                switch (node)
                {
                    case EnumDeclarationSyntax enumDeclaration:
                        _enums.TryAdd(enumDeclaration.Identifier.ValueText, enumDeclaration.Members.Select(m => m.Identifier.ValueText).ToList());
                        break;
                    case TypeDeclarationSyntax type and (ClassDeclarationSyntax or RecordDeclarationSyntax or StructDeclarationSyntax or InterfaceDeclarationSyntax):
                        Add(_types, type.Identifier.ValueText, type);
                        break;
                    case FieldDeclarationSyntax field when field.Modifiers.Any(SyntaxKind.ConstKeyword)
                                                            || field.Modifiers.Any(SyntaxKind.StaticKeyword) && field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword):
                        foreach (var variable in field.Declaration.Variables.Where(v => v.Initializer is not null))
                        {
                            _constants.TryAdd(variable.Identifier.ValueText, variable.Initializer!.Value);
                            if (field.Parent is TypeDeclarationSyntax owner)
                            {
                                _constants.TryAdd($"{owner.Identifier.ValueText}.{variable.Identifier.ValueText}", variable.Initializer.Value);
                            }
                        }

                        break;
                    case MethodDeclarationSyntax method:
                        Add(_methods, method.Identifier.ValueText, method);
                        break;
                }
            }
        }
    }

    public IReadOnlyList<SourceFile> Files { get; }

    public bool HasAssemblyApiController { get; }

    public MethodDeclarationSyntax? FindMethod(string name, SyntaxTree preferredTree) =>
        _methods.TryGetValue(name, out var methods)
            ? methods.FirstOrDefault(m => m.SyntaxTree == preferredTree) ?? methods[0]
            : null;

    /// <summary>Evaluates literal, const, concatenated, interpolated and nameof() string expressions.</summary>
    public string? EvaluateString(ExpressionSyntax? expression, int depth = 0)
    {
        if (expression is null || depth > MaxDepth)
        {
            return null;
        }

        switch (expression)
        {
            case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression):
                return literal.Token.ValueText;
            case ParenthesizedExpressionSyntax parenthesized:
                return EvaluateString(parenthesized.Expression, depth + 1);
            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression):
                var left = EvaluateString(binary.Left, depth + 1);
                var right = EvaluateString(binary.Right, depth + 1);
                return left is null || right is null ? null : left + right;
            case InterpolatedStringExpressionSyntax interpolated:
                var parts = new List<string>();
                foreach (var content in interpolated.Contents)
                {
                    var part = content switch
                    {
                        InterpolatedStringTextSyntax text => text.TextToken.ValueText,
                        InterpolationSyntax hole => EvaluateString(hole.Expression, depth + 1),
                        _ => null
                    };
                    if (part is null)
                    {
                        return null;
                    }

                    parts.Add(part);
                }

                return string.Concat(parts);
            case InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" } } nameOf
                when nameOf.ArgumentList.Arguments.Count == 1:
                var argument = nameOf.ArgumentList.Arguments[0].Expression;
                return argument switch
                {
                    MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
                    IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                    _ => argument.ToString()
                };
            case MemberAccessExpressionSyntax member:
                var qualified = $"{member.Expression.ToString().Split('.').Last()}.{member.Name.Identifier.ValueText}";
                return Constant(qualified, depth) ?? Constant(member.Name.Identifier.ValueText, depth);
            case IdentifierNameSyntax identifierName:
                return Constant(identifierName.Identifier.ValueText, depth);
            default:
                return null;
        }
    }

    private string? Constant(string name, int depth) =>
        _constants.TryGetValue(name, out var value) ? EvaluateString(value, depth + 1) : null;

    public bool IsSimpleType(TypeSyntax? type)
    {
        var (name, isCollection) = Unwrap(type);
        _ = isCollection;
        return name is not null && (SimpleTypes.Contains(name) || _enums.ContainsKey(name));
    }

    public IReadOnlyList<string>? EnumValues(TypeSyntax? type) =>
        Unwrap(type).Name is { } name && _enums.TryGetValue(name, out var values) ? values : null;

    /// <summary>Strips nullable, array and collection wrappers and namespace qualifiers.</summary>
    public static (string? Name, bool IsCollection) Unwrap(TypeSyntax? type)
    {
        var isCollection = false;
        while (true)
        {
            switch (type)
            {
                case NullableTypeSyntax nullable:
                    type = nullable.ElementType;
                    continue;
                case ArrayTypeSyntax array:
                    isCollection = true;
                    type = array.ElementType;
                    continue;
                case QualifiedNameSyntax qualified:
                    type = qualified.Right;
                    continue;
                case AliasQualifiedNameSyntax alias:
                    type = alias.Name;
                    continue;
                case GenericNameSyntax generic when CollectionTypes.Contains(generic.Identifier.ValueText)
                                                    && generic.TypeArgumentList.Arguments.Count == 1:
                    isCollection = true;
                    type = generic.TypeArgumentList.Arguments[0];
                    continue;
                case GenericNameSyntax generic:
                    return (generic.Identifier.ValueText, isCollection);
                case IdentifierNameSyntax identifier:
                    return (identifier.Identifier.ValueText, isCollection);
                case PredefinedTypeSyntax predefined:
                    return (predefined.Keyword.ValueText, isCollection);
                default:
                    return (null, isCollection);
            }
        }
    }

    /// <summary>Builds the JSON shape of a DTO declared in the scanned source, if found.</summary>
    public TypeShape? GetShape(TypeSyntax? type) => GetShape(type, new HashSet<string>(StringComparer.Ordinal), 0);

    private TypeShape? GetShape(TypeSyntax? type, HashSet<string> visiting, int depth)
    {
        var (name, isCollection) = Unwrap(type);
        if (name is null || depth > 3 || SimpleTypes.Contains(name) || _enums.ContainsKey(name)
            || !_types.TryGetValue(name, out var declarations) || !visiting.Add(name))
        {
            return null;
        }

        try
        {
            var properties = new List<PropertyShape>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var declaration in declarations)
            {
                CollectProperties(declaration, properties, seen, visiting, depth, 0);
            }

            return new TypeShape(name, isCollection, properties);
        }
        finally
        {
            visiting.Remove(name);
        }
    }

    private void CollectProperties(
        TypeDeclarationSyntax declaration,
        List<PropertyShape> properties,
        HashSet<string> seen,
        HashSet<string> visiting,
        int depth,
        int inheritanceDepth)
    {
        if (declaration is RecordDeclarationSyntax { ParameterList: { } positional })
        {
            foreach (var parameter in positional.Parameters)
            {
                if (!seen.Add(parameter.Identifier.ValueText))
                {
                    continue;
                }

                var required = parameter.Default is null && parameter.Type is not NullableTypeSyntax
                               || HasAttribute(parameter.AttributeLists, "Required");
                properties.Add(new PropertyShape(
                    JsonName(parameter.AttributeLists, parameter.Identifier.ValueText),
                    parameter.Type?.ToString() ?? "unknown",
                    required,
                    null,
                    GetShape(parameter.Type, visiting, depth + 1),
                    EnumValues(parameter.Type)));
            }
        }

        foreach (var property in declaration.Members.OfType<PropertyDeclarationSyntax>())
        {
            var isPublic = property.Modifiers.Any(SyntaxKind.PublicKeyword) || declaration is InterfaceDeclarationSyntax;
            if (!isPublic || property.Modifiers.Any(SyntaxKind.StaticKeyword)
                          || HasAttribute(property.AttributeLists, "JsonIgnore")
                          || !seen.Add(property.Identifier.ValueText))
            {
                continue;
            }

            var required = property.Modifiers.Any(m => m.ValueText == "required")
                           || HasAttribute(property.AttributeLists, "Required")
                           || HasAttribute(property.AttributeLists, "BindRequired");
            properties.Add(new PropertyShape(
                JsonName(property.AttributeLists, property.Identifier.ValueText),
                property.Type.ToString(),
                required,
                Summary(property),
                GetShape(property.Type, visiting, depth + 1),
                EnumValues(property.Type)));
        }

        // Include properties inherited from base DTOs declared in the scanned source.
        if (inheritanceDepth < 4 && declaration.BaseList is { } baseList)
        {
            foreach (var baseType in baseList.Types)
            {
                var (baseName, _) = Unwrap(baseType.Type);
                if (baseName is not null && _types.TryGetValue(baseName, out var baseDeclarations))
                {
                    foreach (var baseDeclaration in baseDeclarations.Where(d => d is not InterfaceDeclarationSyntax))
                    {
                        CollectProperties(baseDeclaration, properties, seen, visiting, depth, inheritanceDepth + 1);
                    }
                }
            }
        }
    }

    private static string JsonName(SyntaxList<AttributeListSyntax> attributes, string name)
    {
        var jsonAttribute = FindAttribute(attributes, "JsonPropertyName") ?? FindAttribute(attributes, "JsonProperty");
        if (jsonAttribute?.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax literal
            && literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return literal.Token.ValueText;
        }

        return name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];
    }

    public static string AttributeName(AttributeSyntax attribute)
    {
        var value = attribute.Name switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            GenericNameSyntax generic => generic.Identifier.ValueText,
            QualifiedNameSyntax { Right: GenericNameSyntax generic } => generic.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name.Identifier.ValueText,
            _ => attribute.Name.ToString()
        };

        return value.EndsWith("Attribute", StringComparison.Ordinal) ? value[..^"Attribute".Length] : value;
    }

    public static AttributeSyntax? FindAttribute(SyntaxList<AttributeListSyntax> lists, string name) =>
        lists.SelectMany(list => list.Attributes).FirstOrDefault(attribute => AttributeName(attribute) == name);

    public static bool HasAttribute(SyntaxList<AttributeListSyntax> lists, string name) => FindAttribute(lists, name) is not null;

    public static string? Summary(SyntaxNode node) => DocElement(node, "summary", null);

    public static string? ParameterSummary(SyntaxNode node, string parameterName) => DocElement(node, "param", parameterName);

    private static string? DocElement(SyntaxNode node, string elementName, string? nameAttribute)
    {
        var element = node.GetLeadingTrivia()
            .Select(trivia => trivia.GetStructure())
            .OfType<DocumentationCommentTriviaSyntax>()
            .SelectMany(doc => doc.Content.OfType<XmlElementSyntax>())
            .FirstOrDefault(e => e.StartTag.Name.LocalName.ValueText == elementName
                                 && (nameAttribute is null || e.StartTag.Attributes.OfType<XmlNameAttributeSyntax>()
                                     .Any(a => a.Identifier.Identifier.ValueText == nameAttribute)));
        if (element is null)
        {
            return null;
        }

        // Keep the referenced name of <see cref="X"/> / <paramref name="x"/> instead of dropping it.
        var raw = Regex.Replace(element.Content.ToFullString(), @"<\w+\s+(?:cref|name|langword)=""([^""]*)""\s*/>", "$1");
        var text = Regex.Replace(raw, @"<[^>]+>", " ");
        text = Regex.Replace(text, @"^\s*///", " ", RegexOptions.Multiline);
        var result = Regex.Replace(WebUtility.HtmlDecode(text), @"\s+", " ").Trim();
        return result.Length == 0 ? null : result;
    }

    private static void Add<T>(Dictionary<string, List<T>> map, string key, T value)
    {
        if (!map.TryGetValue(key, out var list))
        {
            map[key] = list = new List<T>();
        }

        list.Add(value);
    }
}

internal sealed record SourceFile(string RelativePath, CompilationUnitSyntax Root);
