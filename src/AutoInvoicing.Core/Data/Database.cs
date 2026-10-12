using System.Globalization;
using Microsoft.Data.Sqlite;

namespace AutoInvoicing.Core.Data;

/// <summary>
/// Owns the SQLite file and schema. Money and quantities are stored as TEXT in
/// invariant culture so they round-trip through SQLite as exact decimals
/// (SQLite REAL would turn them into binary floating point).
/// </summary>
public sealed class Database
{
    public string ConnectionString { get; }

    public Database(string filePath)
    {
        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = filePath,
            ForeignKeys = true,
            Pooling = false,
        }.ToString();
    }

    public static string DefaultPath()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoInvoicing");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "shop.db");
    }

    public SqliteConnection Open()
    {
        var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        return conn;
    }

    public void Initialize()
    {
        using var conn = Open();
        conn.Exec(Schema);
        conn.Exec("INSERT OR IGNORE INTO counters(name, next_value) VALUES ('invoice', 1001), ('ticket', 1001)");
    }

    private const string Schema = """
        PRAGMA journal_mode = WAL;

        CREATE TABLE IF NOT EXISTS settings (
            key   TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS counters (
            name       TEXT PRIMARY KEY,
            next_value INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS customers (
            id      INTEGER PRIMARY KEY AUTOINCREMENT,
            name    TEXT NOT NULL,
            phone   TEXT NOT NULL DEFAULT '',
            email   TEXT NOT NULL DEFAULT '',
            address TEXT NOT NULL DEFAULT '',
            notes   TEXT NOT NULL DEFAULT ''
        );

        CREATE TABLE IF NOT EXISTS vehicles (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            customer_id INTEGER NOT NULL REFERENCES customers(id),
            year        INTEGER,
            make        TEXT NOT NULL DEFAULT '',
            model       TEXT NOT NULL DEFAULT '',
            vin         TEXT NOT NULL DEFAULT '',
            plate       TEXT NOT NULL DEFAULT '',
            color       TEXT NOT NULL DEFAULT '',
            engine      TEXT NOT NULL DEFAULT ''
        );
        CREATE INDEX IF NOT EXISTS ix_vehicles_customer ON vehicles(customer_id);

        CREATE TABLE IF NOT EXISTS parts (
            id               INTEGER PRIMARY KEY AUTOINCREMENT,
            part_number      TEXT NOT NULL,
            description      TEXT NOT NULL DEFAULT '',
            vendor           TEXT NOT NULL DEFAULT '',
            location         TEXT NOT NULL DEFAULT '',
            cost             TEXT NOT NULL DEFAULT '0',
            price            TEXT NOT NULL DEFAULT '0',
            quantity_on_hand TEXT NOT NULL DEFAULT '0',
            reorder_level    TEXT NOT NULL DEFAULT '0',
            taxable          INTEGER NOT NULL DEFAULT 1,
            active           INTEGER NOT NULL DEFAULT 1
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ux_parts_number ON parts(part_number);

        CREATE TABLE IF NOT EXISTS inventory_transactions (
            id              INTEGER PRIMARY KEY AUTOINCREMENT,
            part_id         INTEGER NOT NULL REFERENCES parts(id),
            date            TEXT NOT NULL,
            quantity_change TEXT NOT NULL,
            reason          TEXT NOT NULL DEFAULT '',
            invoice_id      INTEGER
        );
        CREATE INDEX IF NOT EXISTS ix_invtx_part ON inventory_transactions(part_id);

        CREATE TABLE IF NOT EXISTS job_tickets (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            number      INTEGER NOT NULL UNIQUE,
            customer_id INTEGER NOT NULL REFERENCES customers(id),
            vehicle_id  INTEGER NOT NULL REFERENCES vehicles(id),
            date_opened TEXT NOT NULL,
            promised    TEXT,
            status      INTEGER NOT NULL DEFAULT 0,
            mileage     INTEGER,
            technician  TEXT NOT NULL DEFAULT '',
            complaint   TEXT NOT NULL DEFAULT '',
            cause       TEXT NOT NULL DEFAULT '',
            correction  TEXT NOT NULL DEFAULT '',
            notes       TEXT NOT NULL DEFAULT ''
        );

        CREATE TABLE IF NOT EXISTS job_ticket_lines (
            id            INTEGER PRIMARY KEY AUTOINCREMENT,
            job_ticket_id INTEGER NOT NULL REFERENCES job_tickets(id) ON DELETE CASCADE,
            sort_order    INTEGER NOT NULL,
            type          INTEGER NOT NULL,
            part_id       INTEGER REFERENCES parts(id),
            part_number   TEXT NOT NULL DEFAULT '',
            description   TEXT NOT NULL DEFAULT '',
            quantity      TEXT NOT NULL,
            unit_price    TEXT NOT NULL,
            unit_cost     TEXT NOT NULL,
            taxable       INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS invoices (
            id               INTEGER PRIMARY KEY AUTOINCREMENT,
            number           INTEGER NOT NULL UNIQUE,
            customer_id      INTEGER NOT NULL REFERENCES customers(id),
            vehicle_id       INTEGER NOT NULL REFERENCES vehicles(id),
            job_ticket_id    INTEGER REFERENCES job_tickets(id),
            invoice_date     TEXT NOT NULL,
            status           INTEGER NOT NULL DEFAULT 0,
            mileage          INTEGER,
            tax_rate_percent TEXT NOT NULL,
            notes            TEXT NOT NULL DEFAULT '',
            void_reason      TEXT NOT NULL DEFAULT ''
        );
        CREATE INDEX IF NOT EXISTS ix_invoices_date ON invoices(invoice_date);
        CREATE INDEX IF NOT EXISTS ix_invoices_customer ON invoices(customer_id);
        CREATE INDEX IF NOT EXISTS ix_invoices_vehicle ON invoices(vehicle_id);

        CREATE TABLE IF NOT EXISTS invoice_lines (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            invoice_id  INTEGER NOT NULL REFERENCES invoices(id) ON DELETE CASCADE,
            sort_order  INTEGER NOT NULL,
            type        INTEGER NOT NULL,
            part_id     INTEGER REFERENCES parts(id),
            part_number TEXT NOT NULL DEFAULT '',
            description TEXT NOT NULL DEFAULT '',
            quantity    TEXT NOT NULL,
            unit_price  TEXT NOT NULL,
            unit_cost   TEXT NOT NULL,
            taxable     INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS payments (
            id         INTEGER PRIMARY KEY AUTOINCREMENT,
            invoice_id INTEGER NOT NULL REFERENCES invoices(id) ON DELETE CASCADE,
            date       TEXT NOT NULL,
            amount     TEXT NOT NULL,
            method     TEXT NOT NULL DEFAULT '',
            reference  TEXT NOT NULL DEFAULT ''
        );
        CREATE INDEX IF NOT EXISTS ix_payments_date ON payments(date);
        """;
}

internal static class SqliteExtensions
{
    private const string DateFormat = "yyyy-MM-dd";
    private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    public static SqliteCommand Command(this SqliteConnection conn, string sql, params (string Name, object? Value)[] args)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args)
            cmd.Parameters.AddWithValue(name, ToDb(value));
        return cmd;
    }

    public static int Exec(this SqliteConnection conn, string sql, params (string, object?)[] args)
    {
        using var cmd = conn.Command(sql, args);
        return cmd.ExecuteNonQuery();
    }

    public static long Insert(this SqliteConnection conn, string sql, params (string, object?)[] args)
    {
        using var cmd = conn.Command(sql + "; SELECT last_insert_rowid();", args);
        return (long)cmd.ExecuteScalar()!;
    }

    public static object? Scalar(this SqliteConnection conn, string sql, params (string, object?)[] args)
    {
        using var cmd = conn.Command(sql, args);
        var result = cmd.ExecuteScalar();
        return result is DBNull ? null : result;
    }

    public static List<T> Query<T>(this SqliteConnection conn, string sql, Func<SqliteDataReader, T> map, params (string, object?)[] args)
    {
        using var cmd = conn.Command(sql, args);
        using var reader = cmd.ExecuteReader();
        var list = new List<T>();
        while (reader.Read())
            list.Add(map(reader));
        return list;
    }

    private static object ToDb(object? value) => value switch
    {
        null => DBNull.Value,
        decimal d => Dec(d),
        DateTime dt => dt.TimeOfDay == TimeSpan.Zero
            ? dt.ToString(DateFormat, CultureInfo.InvariantCulture)
            : dt.ToString(DateTimeFormat, CultureInfo.InvariantCulture),
        bool b => b ? 1 : 0,
        Enum e => Convert.ToInt32(e),
        _ => value,
    };

    public static string Dec(decimal d) => d.ToString(CultureInfo.InvariantCulture);

    public static string Date(DateTime d) => d.ToString(DateFormat, CultureInfo.InvariantCulture);

    public static string Str(this SqliteDataReader r, string col) =>
        r.IsDBNull(r.GetOrdinal(col)) ? "" : r.GetString(r.GetOrdinal(col));

    public static long Long(this SqliteDataReader r, string col) => r.GetInt64(r.GetOrdinal(col));

    public static long? NLong(this SqliteDataReader r, string col) =>
        r.IsDBNull(r.GetOrdinal(col)) ? null : r.GetInt64(r.GetOrdinal(col));

    public static int Int(this SqliteDataReader r, string col) => r.GetInt32(r.GetOrdinal(col));

    public static int? NInt(this SqliteDataReader r, string col) =>
        r.IsDBNull(r.GetOrdinal(col)) ? null : r.GetInt32(r.GetOrdinal(col));

    public static bool Bool(this SqliteDataReader r, string col) => r.GetInt64(r.GetOrdinal(col)) != 0;

    public static decimal Decimal(this SqliteDataReader r, string col) =>
        decimal.Parse(r.GetString(r.GetOrdinal(col)), NumberStyles.Number, CultureInfo.InvariantCulture);

    public static DateTime DateTime(this SqliteDataReader r, string col) =>
        System.DateTime.Parse(r.GetString(r.GetOrdinal(col)), CultureInfo.InvariantCulture);

    public static DateTime? NDateTime(this SqliteDataReader r, string col) =>
        r.IsDBNull(r.GetOrdinal(col)) ? null : r.DateTime(col);
}
