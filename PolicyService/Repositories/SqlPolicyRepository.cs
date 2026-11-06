using System.Globalization;
using Dapper;
using Microsoft.Data.SqlClient;

namespace PolicyService.Repositories;

public sealed class SqlPolicyRepository
{
    private readonly string _connectionString;

    public SqlPolicyRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<string> CreateOfferAsync(
        string productCode,
        string policyFrom,
        string policyTo,
        string priceWire,
        string? agentLogin)
    {
        var from = ParseDate(policyFrom);
        var to = ParseDate(policyTo);
        var (total, covers) = ParsePrice(priceWire);
        var number = Guid.NewGuid().ToString();
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        await conn.ExecuteAsync(
            """
            insert into Offer (Number, ProductCode, PolicyFrom, PolicyTo, TotalPrice, Status, CreationDate, AgentLogin)
            values (@Number, @ProductCode, @PolicyFrom, @PolicyTo, @TotalPrice, N'New', @CreationDate, @AgentLogin)
            """,
            new
            {
                Number = number,
                ProductCode = productCode,
                PolicyFrom = from,
                PolicyTo = to,
                TotalPrice = total,
                CreationDate = DateTime.Now,
                AgentLogin = string.IsNullOrWhiteSpace(agentLogin) ? null : agentLogin
            },
            tx);
        for (var i = 0; i < covers.Count; i++)
        {
            await conn.ExecuteAsync(
                """
                insert into OfferCover (OfferNumber, Code, Price, SortOrder)
                values (@OfferNumber, @Code, @Price, @SortOrder)
                """,
                new { OfferNumber = number, covers[i].Code, covers[i].Price, SortOrder = i },
                tx);
        }

        await tx.CommitAsync();
        return $"{number}|{priceWire}";
    }

    public async Task<string> CreatePolicyAsync(
        string offerNumber,
        string firstName,
        string lastName,
        string taxId,
        string country,
        string zipCode,
        string city,
        string street)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        var offer = await conn.QueryFirstOrDefaultAsync<OfferRow>(
            """
            select Number, ProductCode, PolicyFrom, PolicyTo, TotalPrice, Status, CreationDate,
                   isnull(AgentLogin, N'') as AgentLogin
            from Offer
            where Number = @Number
            """,
            new { Number = offerNumber },
            tx);
        if (offer == null)
        {
            throw new InvalidOperationException($"Offer {offerNumber} not found.");
        }

        if (offer.CreationDate.AddDays(30) < DateTime.Now)
        {
            throw new InvalidOperationException($"Offer {offer.Number} has expired");
        }

        if (offer.Status != "New")
        {
            throw new InvalidOperationException($"Offer {offer.Number} is not in new status and cannot be bought");
        }

        var covers = (await conn.QueryAsync<PricedCover>(
            """
            select Code, Price
            from OfferCover
            where OfferNumber = @Number
            order by SortOrder, Code
            """,
            new { Number = offerNumber },
            tx)).ToList();
        var policyNumber = Guid.NewGuid().ToString();
        var now = DateTime.Now;
        var holder = $"{firstName} {lastName}".Trim();
        await conn.ExecuteAsync(
            """
            insert into Policy (Number, ProductCode, Status, CreationDate, AgentLogin)
            values (@Number, @ProductCode, N'Active', @CreationDate, @AgentLogin)
            """,
            new
            {
                Number = policyNumber,
                offer.ProductCode,
                CreationDate = now,
                AgentLogin = string.IsNullOrWhiteSpace(offer.AgentLogin) ? null : offer.AgentLogin
            },
            tx);
        await InsertVersionAsync(conn, tx, new VersionRow
        {
            PolicyNumber = policyNumber,
            VersionNumber = 1,
            FirstName = firstName ?? string.Empty,
            LastName = lastName ?? string.Empty,
            TaxId = taxId ?? string.Empty,
            Country = country ?? string.Empty,
            ZipCode = zipCode ?? string.Empty,
            City = city ?? string.Empty,
            Street = street ?? string.Empty,
            CoverFrom = offer.PolicyFrom,
            CoverTo = offer.PolicyTo,
            VersionFrom = offer.PolicyFrom,
            VersionTo = offer.PolicyTo,
            TotalPremium = offer.TotalPrice,
            Covers = covers.Select(c => new CoverPeriod
            {
                Code = c.Code,
                Premium = c.Price,
                ValidFrom = offer.PolicyFrom,
                ValidTo = offer.PolicyTo
            }).ToList()
        });
        await conn.ExecuteAsync(
            "update Offer set Status = N'Converted' where Number = @Number",
            new { Number = offerNumber },
            tx);
        var accountNumber = Guid.NewGuid().ToString();
        await conn.ExecuteAsync(
            """
            insert into PolicyAccount (PolicyNumber, AccountNumber, FirstName, LastName, Status)
            values (@PolicyNumber, @AccountNumber, @FirstName, @LastName, N'Active')
            """,
            new { PolicyNumber = policyNumber, AccountNumber = accountNumber, FirstName = firstName, LastName = lastName },
            tx);
        await conn.ExecuteAsync(
            """
            insert into AccountEntry (PolicyNumber, Kind, Amount, EffectiveDate, CreationDate)
            values (@PolicyNumber, N'Expected', @Amount, @EffectiveDate, @CreationDate)
            """,
            new { PolicyNumber = policyNumber, Amount = offer.TotalPrice, EffectiveDate = now, CreationDate = now },
            tx);
        await conn.ExecuteAsync(
            """
            insert into PolicySearch (PolicyNumber, PolicyHolder, ProductCode, Status)
            values (@PolicyNumber, @PolicyHolder, @ProductCode, N'Active')
            """,
            new { PolicyNumber = policyNumber, PolicyHolder = holder, offer.ProductCode },
            tx);
        await conn.ExecuteAsync(
            """
            insert into SalesFact (PolicyNumber, AgentLogin, ProductCode, SalesDate, Premium)
            values (@PolicyNumber, @AgentLogin, @ProductCode, @SalesDate, @Premium)
            """,
            new
            {
                PolicyNumber = policyNumber,
                AgentLogin = offer.AgentLogin ?? string.Empty,
                offer.ProductCode,
                SalesDate = now.Date,
                Premium = offer.TotalPrice
            },
            tx);
        await tx.CommitAsync();
        return policyNumber;
    }

    public async Task<string> GetPolicyDetailsAsync(string policyNumber)
    {
        await using var conn = new SqlConnection(_connectionString);
        var policy = await conn.QueryFirstOrDefaultAsync<PolicyRow>(
            """
            select Number, ProductCode, Status
            from Policy
            where Number = @Number
            """,
            new { Number = policyNumber });
        if (policy == null)
        {
            throw new InvalidOperationException($"Policy {policyNumber} not found!");
        }

        var version = await conn.QueryFirstOrDefaultAsync<VersionRow>(
            """
            select PolicyNumber, VersionNumber, FirstName, LastName, isnull(TaxId, N'') as TaxId,
                   isnull(Country, N'') as Country, isnull(ZipCode, N'') as ZipCode,
                   isnull(City, N'') as City, isnull(Street, N'') as Street,
                   CoverFrom, CoverTo, VersionFrom, VersionTo, TotalPremium
            from PolicyVersion
            where PolicyNumber = @Number and VersionNumber = 1
            """,
            new { Number = policyNumber });
        if (version == null)
        {
            throw new InvalidOperationException($"Policy {policyNumber} not found!");
        }

        var covers = await conn.QueryAsync<string>(
            """
            select Code
            from PolicyCover
            where PolicyNumber = @Number and VersionNumber = 1
            order by SortOrder, Code
            """,
            new { Number = policyNumber });
        return string.Join('|',
            policy.Number,
            policy.ProductCode,
            Date(version.CoverFrom),
            Date(version.CoverTo),
            $"{version.FirstName} {version.LastName}".Trim(),
            Money(version.TotalPremium),
            string.Join(';', covers));
    }

    public async Task<string> TerminateAsync(string policyNumber, string terminationDate)
    {
        var endDate = ParseDate(terminationDate);
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        var policy = await conn.QueryFirstOrDefaultAsync<PolicyRow>(
            "select Number, ProductCode, Status from Policy where Number = @Number",
            new { Number = policyNumber },
            tx);
        if (policy == null)
        {
            throw new InvalidOperationException($"Policy {policyNumber} not found!");
        }

        if (policy.Status != "Active")
        {
            throw new InvalidOperationException($"Policy {policy.Number} is already terminated");
        }

        var versions = (await conn.QueryAsync<VersionRow>(
            """
            select PolicyNumber, VersionNumber, FirstName, LastName, isnull(TaxId, N'') as TaxId,
                   isnull(Country, N'') as Country, isnull(ZipCode, N'') as ZipCode,
                   isnull(City, N'') as City, isnull(Street, N'') as Street,
                   CoverFrom, CoverTo, VersionFrom, VersionTo, TotalPremium
            from PolicyVersion
            where PolicyNumber = @Number
            """,
            new { Number = policyNumber },
            tx)).ToList();
        var coverRows = (await conn.QueryAsync<CoverPeriod>(
            """
            select VersionNumber, Code, Premium, ValidFrom, ValidTo, SortOrder
            from PolicyCover
            where PolicyNumber = @Number
            """,
            new { Number = policyNumber },
            tx)).ToList();
        foreach (var version in versions)
        {
            version.Covers = coverRows.Where(c => c.VersionNumber == version.VersionNumber)
                .OrderBy(c => c.SortOrder)
                .ToList();
        }

        var effective = versions
            .Where(v => Contains(v.VersionFrom, v.VersionTo, endDate))
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefault();
        if (effective == null)
        {
            throw new InvalidOperationException(
                $"No valid policy {policy.Number} version exists at {terminationDate}. Policy cannot be terminated.");
        }

        if (!Contains(effective.CoverFrom, effective.CoverTo, endDate))
        {
            throw new InvalidOperationException(
                $"Policy {policy.Number} does not cover {terminationDate}. Policy cannot be terminated at this date.");
        }

        var endedCovers = effective.Covers.Select(c => new CoverPeriod
        {
            Code = c.Code,
            Premium = EndPremium(c.Premium, c.ValidFrom, c.ValidTo, endDate),
            ValidFrom = c.ValidFrom,
            ValidTo = endDate,
            SortOrder = c.SortOrder
        }).ToList();
        var newTotal = endedCovers.Sum(c => c.Premium);
        var moneyToReturn = effective.TotalPremium - newTotal;
        var nextVersion = versions.Max(v => v.VersionNumber) + 1;
        await InsertVersionAsync(conn, tx, new VersionRow
        {
            PolicyNumber = policyNumber,
            VersionNumber = nextVersion,
            FirstName = effective.FirstName,
            LastName = effective.LastName,
            TaxId = effective.TaxId,
            Country = effective.Country,
            ZipCode = effective.ZipCode,
            City = effective.City,
            Street = effective.Street,
            CoverFrom = effective.CoverFrom,
            CoverTo = endDate,
            VersionFrom = endDate.AddDays(1),
            VersionTo = effective.VersionTo,
            TotalPremium = newTotal,
            Covers = endedCovers
        });
        var now = DateTime.Now;
        await conn.ExecuteAsync(
            "update Policy set Status = N'Terminated' where Number = @Number",
            new { Number = policyNumber },
            tx);
        await conn.ExecuteAsync(
            """
            insert into AccountEntry (PolicyNumber, Kind, Amount, EffectiveDate, CreationDate)
            values (@PolicyNumber, N'Out', @Amount, @EffectiveDate, @CreationDate)
            """,
            new { PolicyNumber = policyNumber, Amount = moneyToReturn, EffectiveDate = now, CreationDate = now },
            tx);
        await conn.ExecuteAsync(
            "update PolicyAccount set Status = N'Terminated' where PolicyNumber = @PolicyNumber",
            new { PolicyNumber = policyNumber },
            tx);
        await conn.ExecuteAsync(
            "update PolicySearch set Status = N'Terminated' where PolicyNumber = @PolicyNumber",
            new { PolicyNumber = policyNumber },
            tx);
        await tx.CommitAsync();
        return $"{policyNumber}|{Money(moneyToReturn)}";
    }

    private static async Task InsertVersionAsync(SqlConnection conn, System.Data.Common.DbTransaction tx, VersionRow version)
    {
        await conn.ExecuteAsync(
            """
            insert into PolicyVersion
                (PolicyNumber, VersionNumber, FirstName, LastName, TaxId, Country, ZipCode, City, Street,
                 CoverFrom, CoverTo, VersionFrom, VersionTo, TotalPremium)
            values
                (@PolicyNumber, @VersionNumber, @FirstName, @LastName, @TaxId, @Country, @ZipCode, @City, @Street,
                 @CoverFrom, @CoverTo, @VersionFrom, @VersionTo, @TotalPremium)
            """,
            new
            {
                version.PolicyNumber,
                version.VersionNumber,
                version.FirstName,
                version.LastName,
                version.TaxId,
                version.Country,
                version.ZipCode,
                version.City,
                version.Street,
                version.CoverFrom,
                version.CoverTo,
                version.VersionFrom,
                version.VersionTo,
                version.TotalPremium
            },
            tx);
        foreach (var cover in version.Covers)
        {
            await conn.ExecuteAsync(
                """
                insert into PolicyCover (PolicyNumber, VersionNumber, Code, Premium, ValidFrom, ValidTo, SortOrder)
                values (@PolicyNumber, @VersionNumber, @Code, @Premium, @ValidFrom, @ValidTo, @SortOrder)
                """,
                new
                {
                    version.PolicyNumber,
                    version.VersionNumber,
                    cover.Code,
                    cover.Premium,
                    cover.ValidFrom,
                    cover.ValidTo,
                    cover.SortOrder
                },
                tx);
        }
    }

    private static (decimal Total, List<PricedCover> Covers) ParsePrice(string priceWire)
    {
        var parts = (priceWire ?? string.Empty).Split('|', 2);
        var total = decimal.Parse(parts[0], CultureInfo.InvariantCulture);
        var covers = new List<PricedCover>();
        if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
        {
            foreach (var part in parts[1].Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = part.IndexOf('=');
                covers.Add(new PricedCover
                {
                    Code = part[..eq],
                    Price = decimal.Parse(part[(eq + 1)..], CultureInfo.InvariantCulture)
                });
            }
        }

        return (total, covers);
    }

    private static decimal EndPremium(decimal premium, DateTime from, DateTime to, DateTime end)
    {
        var originalDays = (to - from).Days;
        var daysNotUsed = originalDays - (end - from).Days;
        return decimal.Round(premium - premium * decimal.Divide(daysNotUsed, originalDays), 2);
    }

    private static bool Contains(DateTime from, DateTime to, DateTime date) => date >= from.Date && date <= to.Date;

    private static DateTime ParseDate(string value)
    {
        if (!DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new InvalidOperationException($"Invalid date '{value}'.");
        }

        return date;
    }

    private static string Date(DateTime value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private sealed class OfferRow
    {
        public string Number { get; set; } = string.Empty;
        public string ProductCode { get; set; } = string.Empty;
        public DateTime PolicyFrom { get; set; }
        public DateTime PolicyTo { get; set; }
        public decimal TotalPrice { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreationDate { get; set; }
        public string AgentLogin { get; set; } = string.Empty;
    }

    private sealed class PolicyRow
    {
        public string Number { get; set; } = string.Empty;
        public string ProductCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    private sealed class VersionRow
    {
        public string PolicyNumber { get; set; } = string.Empty;
        public int VersionNumber { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string TaxId { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public DateTime CoverFrom { get; set; }
        public DateTime CoverTo { get; set; }
        public DateTime VersionFrom { get; set; }
        public DateTime VersionTo { get; set; }
        public decimal TotalPremium { get; set; }
        public List<CoverPeriod> Covers { get; set; } = [];
    }

    private sealed class PricedCover
    {
        public string Code { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    private sealed class CoverPeriod
    {
        public int VersionNumber { get; set; }
        public string Code { get; set; } = string.Empty;
        public decimal Premium { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }
        public int SortOrder { get; set; }
    }
}
