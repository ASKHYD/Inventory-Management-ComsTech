using System;
using System.Text.RegularExpressions;

namespace SOFT2027.UI.Services;

/// <summary>
/// Intercepts raw database, SQLite, and system exceptions and translates them into
/// respectful, plain-language instructions for the non-technical FMCG operator.
/// </summary>
public static class ExceptionTranslator
{
    private static readonly Regex UniqueConstraintRegex = new(
        @"UNIQUE constraint failed:\s*(\w+)\.(\w+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NotNullConstraintRegex = new(
        @"NOT NULL constraint failed:\s*(\w+)\.(\w+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Translate(Exception? ex)
    {
        if (ex == null) return string.Empty;
        return Translate(ex.Message);
    }

    public static string Translate(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return string.Empty;

        var text = message.Trim();

        // 1. UNIQUE constraint
        var uniqueMatch = UniqueConstraintRegex.Match(text);
        if (uniqueMatch.Success)
        {
            var table = uniqueMatch.Groups[1].Value;
            var column = uniqueMatch.Groups[2].Value;
            var friendlyEntity = FriendlyEntityName(table);
            var friendlyColumn = FriendlyColumnName(column);

            return $"A {friendlyEntity} with this {friendlyColumn} already exists. Please enter a different code or name.";
        }

        // 2. FOREIGN KEY constraint
        if (text.Contains("FOREIGN KEY constraint failed", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("foreign key violation", StringComparison.OrdinalIgnoreCase))
        {
            return "This item is linked to existing invoices or ledger entries and cannot be removed. To prevent discrepancies, change its status to Inactive instead.";
        }

        // 3. SQLite disk I/O error
        if (text.Contains("disk I/O error", StringComparison.OrdinalIgnoreCase))
        {
            return "Could not write to database. Please check if your hard drive is full or if another program is locking the file.";
        }

        // 4. Database locked / busy
        if (text.Contains("database is locked", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("database table is locked", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("SQLite Error 5: 'database is locked'", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("busy", StringComparison.OrdinalIgnoreCase))
        {
            return "The database is temporarily locked/busy with another task. Please wait a few seconds and try again.";
        }

        // 5. NOT NULL constraint
        var notNullMatch = NotNullConstraintRegex.Match(text);
        if (notNullMatch.Success)
        {
            var column = notNullMatch.Groups[2].Value;
            var friendlyColumn = FriendlyColumnName(column);
            return $"The {friendlyColumn} field cannot be blank. Please enter the required value.";
        }

        // 6. Generic SQLite syntax / operational error cleanup
        if (text.StartsWith("SQLite Error", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("constraint failed", StringComparison.OrdinalIgnoreCase))
        {
            return "The operation could not be completed due to a database validation check. Please review your entries and try again.";
        }

        return text;
    }

    private static string FriendlyEntityName(string table) => table.ToLowerInvariant() switch
    {
        "products" or "product" => "product",
        "customers" or "customer" => "customer",
        "suppliers" or "supplier" => "supplier",
        "categories" or "category" => "category",
        "brands" or "brand" => "brand",
        "units" or "unit" => "unit",
        "adminusers" or "adminuser" or "users" => "user account",
        _ => "record"
    };

    private static string FriendlyColumnName(string column) => column.ToLowerInvariant() switch
    {
        "productcode" => "Product Code",
        "productname" => "Product Name",
        "barcode" => "Barcode",
        "customercode" => "Customer Code",
        "customername" => "Customer Name",
        "suppliercode" => "Supplier Code",
        "suppliername" => "Supplier Name",
        "username" => "Username",
        "phone" => "Phone Number",
        _ => Regex.Replace(column, "([a-z])([A-Z])", "$1 $2")
    };
}
