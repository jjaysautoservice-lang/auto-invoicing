using System.Globalization;
using AutoInvoicing.Core.Models;

namespace AutoInvoicing.Core.Data;

public sealed class SettingsRepository(Database db)
{
    public ShopSettings Load()
    {
        using var conn = db.Open();
        var values = conn.Query("SELECT key, value FROM settings", r => (Key: r.GetString(0), Value: r.GetString(1)))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        var s = new ShopSettings();
        string Get(string key, string fallback) => values.TryGetValue(key, out var v) ? v : fallback;
        s.ShopName = Get("shop_name", s.ShopName);
        s.Address = Get("address", s.Address);
        s.Phone = Get("phone", s.Phone);
        s.Email = Get("email", s.Email);
        s.TaxId = Get("tax_id", s.TaxId);
        s.TaxRatePercent = decimal.Parse(Get("tax_rate_percent", "0"), CultureInfo.InvariantCulture);
        s.TaxLabor = Get("tax_labor", "0") == "1";
        s.TaxFees = Get("tax_fees", "1") == "1";
        s.LaborRate = decimal.Parse(Get("labor_rate", SqliteExtensions.Dec(s.LaborRate)), CultureInfo.InvariantCulture);
        s.InvoiceFooter = Get("invoice_footer", s.InvoiceFooter);
        return s;
    }

    public void Save(ShopSettings s)
    {
        if (s.TaxRatePercent < 0m)
            throw new ArgumentException("Tax rate cannot be negative.");
        using var conn = db.Open();
        using var tx = conn.BeginTransaction();
        void Set(string key, string value) =>
            conn.Exec("INSERT INTO settings(key, value) VALUES ($k, $v) ON CONFLICT(key) DO UPDATE SET value = excluded.value",
                ("$k", key), ("$v", value));
        Set("shop_name", s.ShopName);
        Set("address", s.Address);
        Set("phone", s.Phone);
        Set("email", s.Email);
        Set("tax_id", s.TaxId);
        Set("tax_rate_percent", SqliteExtensions.Dec(s.TaxRatePercent));
        Set("tax_labor", s.TaxLabor ? "1" : "0");
        Set("tax_fees", s.TaxFees ? "1" : "0");
        Set("labor_rate", SqliteExtensions.Dec(s.LaborRate));
        Set("invoice_footer", s.InvoiceFooter);
        tx.Commit();
    }
}
