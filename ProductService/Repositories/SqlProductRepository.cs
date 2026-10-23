using System.Globalization;
using Dapper;
using Microsoft.Data.SqlClient;

namespace ProductService.Repositories;

public sealed class SqlProductRepository
{
    private readonly string _connectionString;

    public SqlProductRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<string[]> FindAllActiveAsync()
    {
        await using var conn = new SqlConnection(_connectionString);
        var rows = await conn.QueryAsync<ProductRow>(
            """
            select Code, Name, isnull(Description, N'') as Description, isnull(Image, N'') as Image,
                   MaxNumberOfInsured, isnull(Icon, N'') as Icon, Status
            from Product
            where Status = N'Active'
            order by Code
            """);
        return rows.Select(FormatSummary).ToArray();
    }

    public async Task<string> FindByCodeAsync(string productCode)
    {
        await using var conn = new SqlConnection(_connectionString);
        var product = await LoadAsync(conn, null, productCode);
        return product == null ? string.Empty : FormatDetail(product);
    }

    public async Task<string> CreateDraftAsync(
        string code,
        string name,
        string image,
        string description,
        int maxNumberOfInsured,
        string icon,
        string covers,
        string questions)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("Product code must not be empty.");
        }

        var coverRows = ParseCovers(covers);
        var questionRows = ParseQuestions(questions);

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        var existing = await conn.ExecuteScalarAsync<int>(
            "select count(1) from Product where Code = @Code",
            new { Code = code },
            tx);
        if (existing > 0)
        {
            throw new InvalidOperationException($"Product {code} already exists.");
        }

        await conn.ExecuteAsync(
            """
            insert into Product (Code, Name, Image, Description, MaxNumberOfInsured, Icon, Status)
            values (@Code, @Name, @Image, @Description, @MaxNumberOfInsured, @Icon, N'Draft')
            """,
            new
            {
                Code = code,
                Name = name ?? string.Empty,
                Image = image ?? string.Empty,
                Description = description ?? string.Empty,
                MaxNumberOfInsured = maxNumberOfInsured,
                Icon = icon ?? string.Empty
            },
            tx);

        await InsertChildrenAsync(conn, tx, code, coverRows, questionRows);
        await tx.CommitAsync();
        return code;
    }

    public async Task<string> ActivateAsync(string productCode)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        var status = await conn.ExecuteScalarAsync<string>(
            "select Status from Product where Code = @Code",
            new { Code = productCode });
        if (status == null)
        {
            throw new InvalidOperationException($"Product {productCode} not found.");
        }

        if (status != "Draft")
        {
            throw new InvalidOperationException("Only draft version can be modified and activated");
        }

        await conn.ExecuteAsync(
            "update Product set Status = N'Active' where Code = @Code",
            new { Code = productCode });
        return productCode;
    }

    public async Task<string> DiscontinueAsync(string productCode)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        var updated = await conn.ExecuteAsync(
            "update Product set Status = N'Discontinued' where Code = @Code",
            new { Code = productCode });
        if (updated == 0)
        {
            throw new InvalidOperationException($"Product {productCode} not found.");
        }

        return productCode;
    }

    private static async Task InsertChildrenAsync(
        SqlConnection conn,
        System.Data.Common.DbTransaction tx,
        string code,
        IReadOnlyList<CoverRow> covers,
        IReadOnlyList<QuestionRow> questions)
    {
        for (var i = 0; i < covers.Count; i++)
        {
            var cover = covers[i];
            await conn.ExecuteAsync(
                """
                insert into ProductCover (ProductCode, Code, Name, Description, Optional, SumInsured, SortOrder)
                values (@ProductCode, @Code, @Name, @Description, @Optional, @SumInsured, @SortOrder)
                """,
                new
                {
                    ProductCode = code,
                    cover.Code,
                    cover.Name,
                    cover.Description,
                    cover.Optional,
                    cover.SumInsured,
                    SortOrder = i
                },
                tx);
        }

        foreach (var question in questions)
        {
            await conn.ExecuteAsync(
                """
                insert into ProductQuestion (ProductCode, Code, Kind, QuestionIndex, Text)
                values (@ProductCode, @Code, @Kind, @QuestionIndex, @Text)
                """,
                new
                {
                    ProductCode = code,
                    question.Code,
                    question.Kind,
                    question.QuestionIndex,
                    question.Text
                },
                tx);
            foreach (var choice in question.Choices)
            {
                await conn.ExecuteAsync(
                    """
                    insert into ProductChoice (ProductCode, QuestionCode, Code, Label)
                    values (@ProductCode, @QuestionCode, @Code, @Label)
                    """,
                    new
                    {
                        ProductCode = code,
                        QuestionCode = question.Code,
                        choice.Code,
                        choice.Label
                    },
                    tx);
            }
        }
    }

    private static async Task<ProductDetail?> LoadAsync(SqlConnection conn, System.Data.Common.DbTransaction? tx, string code)
    {
        var product = await conn.QueryFirstOrDefaultAsync<ProductRow>(
            """
            select Code, Name, isnull(Description, N'') as Description, isnull(Image, N'') as Image,
                   MaxNumberOfInsured, isnull(Icon, N'') as Icon, Status
            from Product
            where Code = @Code
            """,
            new { Code = code },
            tx);
        if (product == null)
        {
            return null;
        }

        var covers = (await conn.QueryAsync<CoverRow>(
            """
            select Code, Name, isnull(Description, N'') as Description, Optional, SumInsured
            from ProductCover
            where ProductCode = @Code
            order by SortOrder, Code
            """,
            new { Code = code },
            tx)).ToList();
        var questions = (await conn.QueryAsync<QuestionRow>(
            """
            select Code, Kind, QuestionIndex, Text
            from ProductQuestion
            where ProductCode = @Code
            order by QuestionIndex, Code
            """,
            new { Code = code },
            tx)).ToList();
        var choices = (await conn.QueryAsync<ChoiceRow>(
            """
            select QuestionCode, Code, Label
            from ProductChoice
            where ProductCode = @Code
            order by QuestionCode, Code
            """,
            new { Code = code },
            tx)).ToList();
        foreach (var question in questions)
        {
            question.Choices = choices.Where(c => c.QuestionCode == question.Code).ToList();
        }

        return new ProductDetail(product, covers, questions);
    }

    private static string FormatSummary(ProductRow product) =>
        string.Join('|',
            product.Code,
            product.Name,
            product.Description,
            product.Image,
            product.MaxNumberOfInsured.ToString(CultureInfo.InvariantCulture),
            product.Icon,
            product.Status);

    private static string FormatDetail(ProductDetail detail)
    {
        var covers = string.Join(';', detail.Covers.Select(c =>
            string.Join('~',
                c.Code,
                c.Name,
                c.Description,
                c.Optional ? "true" : "false",
                c.SumInsured.HasValue ? Money(c.SumInsured.Value) : string.Empty)));
        var questions = string.Join(';', detail.Questions.Select(q =>
        {
            var choices = string.Join(',', q.Choices.Select(c => $"{c.Code}={c.Label}"));
            return string.Join('~', q.Code, q.Kind, q.QuestionIndex.ToString(CultureInfo.InvariantCulture), q.Text, choices);
        }));
        return string.Join('|', FormatSummary(detail.Product), covers, questions);
    }

    private static List<CoverRow> ParseCovers(string? text)
    {
        var list = new List<CoverRow>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return list;
        }

        foreach (var part in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var fields = part.Split('~');
            if (fields.Length < 5)
            {
                throw new InvalidOperationException($"Invalid cover '{part}'.");
            }

            decimal? sum = string.IsNullOrWhiteSpace(fields[4])
                ? null
                : decimal.Parse(fields[4], CultureInfo.InvariantCulture);
            list.Add(new CoverRow
            {
                Code = fields[0],
                Name = fields[1],
                Description = fields[2],
                Optional = bool.Parse(fields[3]),
                SumInsured = sum
            });
        }

        return list;
    }

    private static List<QuestionRow> ParseQuestions(string? text)
    {
        var list = new List<QuestionRow>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return list;
        }

        foreach (var part in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var fields = part.Split('~', 5);
            if (fields.Length < 5)
            {
                throw new InvalidOperationException($"Invalid question '{part}'.");
            }

            var kind = fields[1];
            if (kind is not ("choice" or "numeric" or "date"))
            {
                throw new InvalidOperationException($"Invalid question kind '{kind}'.");
            }

            var choices = new List<ChoiceRow>();
            if (!string.IsNullOrWhiteSpace(fields[4]))
            {
                foreach (var choice in fields[4].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var eq = choice.IndexOf('=');
                    if (eq <= 0)
                    {
                        throw new InvalidOperationException($"Invalid choice '{choice}'.");
                    }

                    choices.Add(new ChoiceRow
                    {
                        Code = choice[..eq],
                        Label = choice[(eq + 1)..]
                    });
                }
            }

            list.Add(new QuestionRow
            {
                Code = fields[0],
                Kind = kind,
                QuestionIndex = int.Parse(fields[2], CultureInfo.InvariantCulture),
                Text = fields[3],
                Choices = choices
            });
        }

        return list;
    }

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private sealed class ProductDetail(ProductRow product, List<CoverRow> covers, List<QuestionRow> questions)
    {
        public ProductRow Product { get; } = product;
        public List<CoverRow> Covers { get; } = covers;
        public List<QuestionRow> Questions { get; } = questions;
    }

    private sealed class ProductRow
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public int MaxNumberOfInsured { get; set; }
        public string Icon { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    private sealed class CoverRow
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Optional { get; set; }
        public decimal? SumInsured { get; set; }
    }

    private sealed class QuestionRow
    {
        public string Code { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public int QuestionIndex { get; set; }
        public string Text { get; set; } = string.Empty;
        public List<ChoiceRow> Choices { get; set; } = [];
    }

    private sealed class ChoiceRow
    {
        public string QuestionCode { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }
}
