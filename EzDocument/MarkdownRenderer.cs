using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp;

namespace EzDocument;

internal static class MarkdownRenderer
{
    public static string RenderGroup(EndpointGroup group)
    {
        var md = new StringBuilder();
        var title = group.Kind == EndpointGroupKind.MinimalApi ? $"{group.Name} (minimal APIs)" : group.Name;
        md.Append("# ").AppendLine(title).AppendLine();
        if (!string.IsNullOrWhiteSpace(group.Summary))
        {
            md.AppendLine(group.Summary).AppendLine();
        }

        md.Append("Source: `").Append(group.SourcePath).AppendLine("`").AppendLine();

        if (group.Endpoints.Count == 0)
        {
            md.AppendLine("No routable actions were found in this controller.");
            return md.ToString();
        }

        md.AppendLine("## Endpoints").AppendLine();
        md.AppendLine("| Method | Route | Request body | Parameters | Auth | Description |");
        md.AppendLine("| --- | --- | --- | --- | --- | --- |");
        foreach (var endpoint in group.Endpoints)
        {
            md.Append("| `").Append(endpoint.Method).Append("` | `").Append(Cell(endpoint.Route)).Append("` | ")
                .Append(BodyCell(endpoint)).Append(" | ")
                .Append(ParameterCell(endpoint)).Append(" | ")
                .Append(endpoint.RequiresAuthorization ? "Required" : "-").Append(" | ")
                .Append(Cell(endpoint.Summary ?? string.Empty)).AppendLine(" |");
        }

        foreach (var endpoint in group.Endpoints)
        {
            RenderEndpoint(md, endpoint);
        }

        return md.ToString();
    }

    private static void RenderEndpoint(StringBuilder md, EndpointInfo endpoint)
    {
        md.AppendLine();
        md.Append("## ").Append(endpoint.Method).Append(' ').Append(endpoint.Route).AppendLine().AppendLine();
        if (!string.IsNullOrWhiteSpace(endpoint.Summary))
        {
            md.AppendLine(endpoint.Summary).AppendLine();
        }

        md.Append("- **Handler:** `").Append(endpoint.Handler).AppendLine("`");
        md.Append("- **Request body:** ").AppendLine(BodyDescription(endpoint));
        md.Append("- **Parameters:** ").AppendLine(endpoint.HasParameters ? ParameterCell(endpoint) : "none");
        md.Append("- **Authorization:** ").AppendLine(endpoint.RequiresAuthorization ? "required" : "not declared");
        if (endpoint.IsConventionalRoute)
        {
            md.AppendLine("- **Routing:** conventional; route assumes the default `{controller}/{action}/{id?}` pattern");
        }

        ParameterTable(md, "Route parameters", endpoint.ParametersFrom(ParameterSource.Route));
        ParameterTable(md, "Query parameters", endpoint.ParametersFrom(ParameterSource.Query));
        ParameterTable(md, "Headers", endpoint.ParametersFrom(ParameterSource.Header));

        if (endpoint.Body is { } body)
        {
            md.AppendLine().AppendLine("### Request body").AppendLine();
            md.Append('`').Append(body.Type).Append("` (").Append(IsRawBody(body) ? "raw request stream" : "`application/json`").Append("), ")
                .AppendLine(body.Required ? "required." : "optional.");
            if (!string.IsNullOrWhiteSpace(body.Summary))
            {
                md.AppendLine().AppendLine(body.Summary);
            }

            ShapeTable(md, body.Shape);
            if (body.Shape is { Properties.Count: > 0 })
            {
                md.AppendLine().AppendLine("```json").AppendLine(JsonExample(body.Shape)).AppendLine("```");
            }
        }

        var form = endpoint.ParametersFrom(ParameterSource.Form).ToList();
        if (form.Count > 0)
        {
            md.AppendLine().AppendLine("### Form fields (`multipart/form-data`)").AppendLine();
            md.AppendLine("| Name | Type | Required | Description |").AppendLine("| --- | --- | --- | --- |");
            foreach (var field in form)
            {
                if (field.Shape is { Properties.Count: > 0 })
                {
                    foreach (var (path, property) in Flatten(field.Shape, prefix: null))
                    {
                        md.Append("| `").Append(path).Append("` | `").Append(Cell(property.Type)).Append("` | ")
                            .Append(property.Required ? "Yes" : "No").Append(" | ").Append(Cell(property.Summary ?? string.Empty)).AppendLine(" |");
                    }
                }
                else
                {
                    md.Append("| `").Append(field.Name).Append("` | `").Append(Cell(field.Type)).Append("` | ")
                        .Append(field.Required ? "Yes" : "No").Append(" | ").Append(Cell(field.Summary ?? string.Empty)).AppendLine(" |");
                }
            }
        }

        if (endpoint.Responses.Count > 0)
        {
            md.AppendLine().AppendLine("### Responses").AppendLine();
            md.AppendLine("| Status | Type |").AppendLine("| --- | --- |");
            foreach (var response in endpoint.Responses)
            {
                md.Append("| ").Append(response.StatusCode).Append(" | ")
                    .Append(response.Type is null ? "-" : $"`{Cell(response.Type)}`").AppendLine(" |");
            }
        }
        else if (endpoint.ResponseType is not null)
        {
            md.AppendLine().Append("**Returns:** `").Append(endpoint.ResponseType).AppendLine("`");
        }
    }

    public static string RenderIndex(IReadOnlyList<(EndpointGroup Group, string Link)> groups, string sourceName)
    {
        var endpoints = groups.SelectMany(g => g.Group.Endpoints.Select(e => (g.Group, g.Link, Endpoint: e))).ToList();
        var md = new StringBuilder();
        md.AppendLine("# API endpoints").AppendLine();
        md.Append("Generated by EzDocument from `").Append(sourceName).Append("` on ")
            .Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'")).AppendLine(".").AppendLine();
        md.Append("- **").Append(endpoints.Count).AppendLine("** endpoints")
            .Append("- **").Append(endpoints.Count(e => e.Endpoint.RequiresBody)).AppendLine("** require a request body")
            .Append("- **").Append(endpoints.Count(e => e.Endpoint.HasParameters)).AppendLine("** take route, query or header parameters")
            .Append("- **").Append(endpoints.Count(e => !e.Endpoint.AcceptsBody && !e.Endpoint.HasParameters)).AppendLine("** take no input")
            .AppendLine();

        if (endpoints.Count == 0)
        {
            md.AppendLine("No ASP.NET Core endpoints were found.");
            return md.ToString();
        }

        md.AppendLine("| Method | Route | Request body | Parameters | Auth | Defined in |");
        md.AppendLine("| --- | --- | --- | --- | --- | --- |");
        foreach (var (group, link, endpoint) in endpoints
                     .OrderBy(e => e.Endpoint.Route, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(e => MethodOrder(e.Endpoint.Method)))
        {
            md.Append("| `").Append(endpoint.Method).Append("` | `").Append(Cell(endpoint.Route)).Append("` | ")
                .Append(BodyCell(endpoint)).Append(" | ")
                .Append(ParameterCell(endpoint)).Append(" | ")
                .Append(endpoint.RequiresAuthorization ? "Required" : "-").Append(" | ")
                .Append('[').Append(Cell(group.Name)).Append("](").Append(link.Replace(" ", "%20", StringComparison.Ordinal)).AppendLine(") |");
        }

        md.AppendLine().AppendLine("Parameter sources: `route:` path segment, `query:` query string, `header:` HTTP header. `?` marks an optional value.");
        return md.ToString();
    }

    private static int MethodOrder(string method) => method switch
    {
        "GET" => 0, "POST" => 1, "PUT" => 2, "PATCH" => 3, "DELETE" => 4, _ => 5
    };

    private static bool IsRawBody(EndpointParameter body) =>
        SourceIndex.Unwrap(SyntaxFactory.ParseTypeName(body.Type)).Name is "Stream" or "PipeReader";

    private static string BodyCell(EndpointInfo endpoint)
    {
        if (endpoint.Body is { } body)
        {
            return $"{(body.Required ? "**Required**" : "Optional")} `{Cell(body.Type)}`";
        }

        if (endpoint.HasForm)
        {
            return $"{(endpoint.RequiresBody ? "**Required**" : "Optional")} form";
        }

        return "-";
    }

    private static string BodyDescription(EndpointInfo endpoint) =>
        endpoint.Body is { } body
            ? $"{(body.Required ? "required" : "optional")} - `{body.Type}` ({(IsRawBody(body) ? "raw stream" : "JSON")})"
            : endpoint.HasForm
                ? $"{(endpoint.RequiresBody ? "required" : "optional")} - form fields (`multipart/form-data`)"
                : "none";

    private static string ParameterCell(EndpointInfo endpoint)
    {
        var parts = endpoint.Parameters
            .Where(p => p.Source is ParameterSource.Route or ParameterSource.Query or ParameterSource.Header)
            .Select(p => $"{p.Source.ToString().ToLowerInvariant()}: `{p.Name}{(p.Required ? string.Empty : "?")}`")
            .ToList();
        return parts.Count == 0 ? "-" : string.Join(", ", parts);
    }

    private static void ParameterTable(StringBuilder md, string title, IEnumerable<EndpointParameter> parameters)
    {
        var list = parameters.ToList();
        if (list.Count == 0)
        {
            return;
        }

        md.AppendLine().Append("### ").AppendLine(title).AppendLine();
        md.AppendLine("| Name | Type | Required | Description |").AppendLine("| --- | --- | --- | --- |");
        foreach (var parameter in list)
        {
            md.Append("| `").Append(parameter.Name).Append("` | `").Append(Cell(parameter.Type)).Append("` | ")
                .Append(parameter.Required ? "Yes" : "No").Append(" | ")
                .Append(Cell(Describe(parameter.Summary, parameter.EnumValues))).AppendLine(" |");
        }
    }

    private static string Describe(string? summary, IReadOnlyList<string>? enumValues)
    {
        if (enumValues is not { Count: > 0 })
        {
            return summary ?? string.Empty;
        }

        var values = "One of: " + string.Join(", ", enumValues.Select(v => $"`{v}`"));
        return string.IsNullOrWhiteSpace(summary) ? values : $"{summary} {values}";
    }

    private static void ShapeTable(StringBuilder md, TypeShape? shape)
    {
        if (shape is not { Properties.Count: > 0 })
        {
            return;
        }

        md.AppendLine().AppendLine("| Field | Type | Required | Description |").AppendLine("| --- | --- | --- | --- |");
        foreach (var (path, property) in Flatten(shape, shape.IsCollection ? "[]" : null))
        {
            md.Append("| `").Append(path).Append("` | `").Append(Cell(property.Type)).Append("` | ")
                .Append(property.Required ? "Yes" : "No").Append(" | ")
                .Append(Cell(Describe(property.Summary, property.EnumValues))).AppendLine(" |");
        }
    }

    private static IEnumerable<(string Path, PropertyShape Property)> Flatten(TypeShape shape, string? prefix)
    {
        foreach (var property in shape.Properties)
        {
            var path = prefix is null ? property.JsonName : $"{prefix}.{property.JsonName}";
            yield return (path, property);
            if (property.Nested is { } nested)
            {
                foreach (var child in Flatten(nested, nested.IsCollection ? path + "[]" : path))
                {
                    yield return child;
                }
            }
        }
    }

    internal static string JsonExample(TypeShape shape)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            if (shape.IsCollection)
            {
                writer.WriteStartArray();
                WriteObject(writer, shape);
                writer.WriteEndArray();
            }
            else
            {
                WriteObject(writer, shape);
            }
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteObject(Utf8JsonWriter writer, TypeShape shape)
    {
        writer.WriteStartObject();
        foreach (var property in shape.Properties)
        {
            writer.WritePropertyName(property.JsonName);
            var (name, isCollection) = SourceIndex.Unwrap(SyntaxFactory.ParseTypeName(property.Type));
            if (isCollection)
            {
                writer.WriteStartArray();
            }

            if (property.Nested is { } nested)
            {
                WriteObject(writer, nested);
            }
            else if (property.EnumValues is not null)
            {
                writer.WriteNumberValue(0); // System.Text.Json serializes enums as numbers by default.
            }
            else
            {
                WriteSample(writer, name);
            }

            if (isCollection)
            {
                writer.WriteEndArray();
            }
        }

        writer.WriteEndObject();
    }

    private static void WriteSample(Utf8JsonWriter writer, string? typeName)
    {
        switch (typeName)
        {
            case "bool" or "Boolean":
                writer.WriteBooleanValue(false);
                break;
            case "int" or "Int32" or "long" or "Int64" or "short" or "Int16" or "byte" or "Byte" or "uint" or "ulong" or "ushort" or "sbyte":
                writer.WriteNumberValue(0);
                break;
            case "decimal" or "Decimal" or "double" or "Double" or "float" or "Single":
                writer.WriteNumberValue(0.0m);
                break;
            case "Guid":
                writer.WriteStringValue("3fa85f64-5717-4562-b3fc-2c963f66afa6");
                break;
            case "DateTime" or "DateTimeOffset":
                writer.WriteStringValue("2026-01-01T00:00:00Z");
                break;
            case "DateOnly":
                writer.WriteStringValue("2026-01-01");
                break;
            case "TimeOnly" or "TimeSpan":
                writer.WriteStringValue("00:00:00");
                break;
            case "string" or "String" or "char" or "Char" or "Uri":
                writer.WriteStringValue("string");
                break;
            case "Dictionary" or "IDictionary" or "IReadOnlyDictionary":
                writer.WriteStartObject();
                writer.WriteEndObject();
                break;
            case "object" or "JsonElement" or "JsonNode":
                writer.WriteNullValue();
                break;
            default:
                // Enums serialize as numbers by default; unknown types fall back to a placeholder.
                writer.WriteStringValue(typeName ?? "value");
                break;
        }
    }

    private static string Cell(string text) =>
        text.Replace("|", "\\|", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}
