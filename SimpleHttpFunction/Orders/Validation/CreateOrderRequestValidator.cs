using SimpleHttpFunction.Orders.Contracts;

namespace SimpleHttpFunction.Orders.Validation;

// =============================================================================
// VALIDATION — reject bad input at the edge, with clear per-field messages.
// =============================================================================
// Kept as its own class so it is unit-testable in isolation and so functions
// stay thin. In larger apps you'd often use FluentValidation; here we do it by
// hand to keep dependencies minimal and the logic obvious to readers.

public static class CreateOrderRequestValidator
{
    public static IReadOnlyList<string> Validate(CreateOrderRequest? req)
    {
        var errors = new List<string>();

        if (req is null)
        {
            errors.Add("Request body is required.");
            return errors; // nothing more to check
        }

        if (string.IsNullOrWhiteSpace(req.CustomerId))
            errors.Add("customerId is required.");

        if (string.IsNullOrWhiteSpace(req.Currency) || req.Currency!.Length != 3)
            errors.Add("currency must be a 3-letter ISO code (e.g. 'USD').");

        if (req.Lines is null || req.Lines.Count == 0)
        {
            errors.Add("At least one order line is required.");
        }
        else
        {
            for (var i = 0; i < req.Lines.Count; i++)
            {
                var line = req.Lines[i];
                if (string.IsNullOrWhiteSpace(line.Sku))
                    errors.Add($"lines[{i}].sku is required.");
                if (line.Quantity <= 0)
                    errors.Add($"lines[{i}].quantity must be greater than 0.");
                if (line.UnitPrice < 0)
                    errors.Add($"lines[{i}].unitPrice cannot be negative.");
            }
        }

        return errors;
    }
}
