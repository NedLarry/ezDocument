using SampleApi.Models;

namespace SampleApi.Endpoints;

public record CreateProductRequest(string Name, decimal Price, string? Description);
public record ProductFilter(string? Search, int Page = 1);

public interface IProductStore { }

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/products").WithTags("Products");

        group.MapGet("/", ([AsParameters] ProductFilter filter, IProductStore store) => Results.Ok())
            .WithSummary("Search the product catalogue.");

        group.MapGet("/{id:int}", GetProduct);

        group.MapPost("/", (CreateProductRequest request, IProductStore store) => Results.Created("", request))
            .RequireAuthorization()
            .WithSummary("Create a product.");

        group.MapDelete("/{id:int}", (int id) => Results.NoContent()).RequireAuthorization();
    }

    /// <summary>Fetches a product by id.</summary>
    /// <param name="id">Product id.</param>
    /// <param name="includeStock">Include live stock levels.</param>
    private static IResult GetProduct(int id, bool? includeStock, HttpContext context) => Results.Ok();
}
