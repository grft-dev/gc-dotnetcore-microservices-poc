using System.Globalization;
using DynamicExpresso;

namespace PricingService.Domain;

internal static class TariffCalculator
{
    public static IReadOnlyList<(string Code, decimal Price)> Calculate(
        DateTime policyFrom,
        DateTime policyTo,
        IReadOnlyList<string> selectedCovers,
        IReadOnlyDictionary<string, object> answers,
        IReadOnlyList<BaseRule> baseRules,
        IReadOnlyList<MarkupRule> markups)
    {
        var covers = selectedCovers.Select(code => new CoverPrice(code, 0m)).ToList();
        var parameters = BuildParameters(policyFrom, policyTo, covers, answers);

        foreach (var cover in covers)
        {
            var rule = baseRules.FirstOrDefault(r =>
                r.CoverCode == cover.Code && Applies(r.ApplyIfFormula, parameters));
            if (rule != null)
            {
                cover.Price = EvalDecimal(rule.BasePriceFormula, parameters);
            }
        }

        foreach (var markup in markups)
        {
            if (!Applies(markup.ApplyIfFormula, parameters))
            {
                continue;
            }

            foreach (var cover in covers)
            {
                cover.Price = decimal.Round(cover.Price * markup.ParamValue, 2);
            }
        }

        return covers.Select(c => (c.Code, c.Price)).ToList();
    }

    public static string Format(IReadOnlyList<(string Code, decimal Price)> prices)
    {
        var total = prices.Sum(p => p.Price);
        var parts = string.Join(';', prices.Select(p => $"{p.Code}={Money(p.Price)}"));
        return $"{Money(total)}|{parts}";
    }

    public static Dictionary<string, object> ParseAnswers(string? answers)
    {
        var subject = new Dictionary<string, object>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(answers))
        {
            return subject;
        }

        foreach (var part in answers.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0)
            {
                throw new InvalidOperationException($"Invalid answer '{part}'.");
            }

            var code = part[..eq];
            if (subject.ContainsKey(code))
            {
                throw new InvalidOperationException($"Answer {code} is duplicated.");
            }

            var raw = part[(eq + 1)..];
            subject[code] = decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
                ? number
                : raw;
        }

        return subject;
    }

    public static List<string> ParseCovers(string? selectedCovers)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(selectedCovers))
        {
            return list;
        }

        foreach (var code in selectedCovers.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (list.Contains(code, StringComparer.Ordinal))
            {
                throw new InvalidOperationException($"Cover {code} is duplicated.");
            }

            list.Add(code);
        }

        return list;
    }

    private static (List<Parameter> Parameters, List<object> Values) BuildParameters(
        DateTime policyFrom,
        DateTime policyTo,
        List<CoverPrice> covers,
        IReadOnlyDictionary<string, object> answers)
    {
        var parameters = new List<Parameter>
        {
            new("policyFrom", typeof(DateTimeOffset)),
            new("policyTo", typeof(DateTimeOffset))
        };
        var values = new List<object>
        {
            new DateTimeOffset(policyFrom),
            new DateTimeOffset(policyTo)
        };
        foreach (var cover in covers)
        {
            parameters.Add(new Parameter(cover.Code, typeof(CoverPrice)));
            values.Add(cover);
        }

        foreach (var answer in answers)
        {
            parameters.Add(new Parameter(answer.Key, answer.Value.GetType()));
            values.Add(answer.Value);
        }

        return (parameters, values);
    }

    private static bool Applies(string? formula, (List<Parameter> Parameters, List<object> Values) parameters)
    {
        if (string.IsNullOrEmpty(formula))
        {
            return true;
        }

        return (bool)new Interpreter()
            .Parse(formula, parameters.Parameters.ToArray())
            .Invoke(parameters.Values.ToArray());
    }

    private static decimal EvalDecimal(string formula, (List<Parameter> Parameters, List<object> Values) parameters) =>
        (decimal)new Interpreter()
            .Parse(formula, parameters.Parameters.ToArray())
            .Invoke(parameters.Values.ToArray());

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    internal sealed class CoverPrice(string code, decimal price)
    {
        public string Code { get; } = code;
        public decimal Price { get; set; } = price;
    }
}

internal sealed class BaseRule
{
    public string CoverCode { get; set; } = string.Empty;
    public string? ApplyIfFormula { get; set; }
    public string BasePriceFormula { get; set; } = string.Empty;
}

internal sealed class MarkupRule
{
    public string? ApplyIfFormula { get; set; }
    public decimal ParamValue { get; set; }
}
