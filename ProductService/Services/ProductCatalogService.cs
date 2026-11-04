using Insurance.Graftcode;
using ProductService.Repositories;

namespace ProductService.Services;

public static class ProductCatalogService
{
    // Graftcode-facing method replacing GET /api/Products.
    public static string[] FindAllProducts() =>
        GraftAsync.Run(() => Repository().FindAllActiveAsync());

    // Graftcode-facing method replacing GET /api/Products/{code}.
    public static string FindProductByCode(string productCode) =>
        GraftAsync.Run(() => Repository().FindByCodeAsync(productCode));

    // Graftcode-facing method replacing POST /api/Products.
    public static string CreateProductDraft(
        string code,
        string name,
        string image,
        string description,
        int maxNumberOfInsured,
        string icon,
        string covers,
        string questions) =>
        GraftAsync.Run(() => Repository().CreateDraftAsync(
            code, name, image, description, maxNumberOfInsured, icon, covers, questions));

    // Graftcode-facing method replacing POST /activate.
    public static string ActivateProduct(string productCode) =>
        GraftAsync.Run(() => Repository().ActivateAsync(productCode));

    // Graftcode-facing method replacing POST /discontinue.
    public static string DiscontinueProduct(string productCode) =>
        GraftAsync.Run(() => Repository().DiscontinueAsync(productCode));

    private static SqlProductRepository Repository() =>
        new(Required("PRODUCT_CN"));

    private static string Required(string key)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Missing {key} environment variable.");
        }

        return value;
    }
}
