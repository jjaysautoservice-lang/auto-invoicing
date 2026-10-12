using System.ComponentModel;
using System.Globalization;
using AutoInvoicing.Core;

namespace AutoInvoicing.App;

/// <summary>Small helpers so the code-only forms stay short and consistent.</summary>
internal static class Ui
{
    public static readonly Font HeaderFont = new("Segoe UI", 11f, FontStyle.Bold);
    public static readonly Font TotalFont = new("Segoe UI", 12f, FontStyle.Bold);

    public static void ShowError(Exception ex) =>
        MessageBox.Show(ex is ArgumentException or InvalidOperationException ? ex.Message : ex.ToString(),
            "Auto Invoicing", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    /// <summary>Runs an action and shows a friendly message instead of crashing on validation errors.</summary>
    public static bool Try(Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return false;
        }
    }

    public static bool Confirm(string message) =>
        MessageBox.Show(message, "Auto Invoicing", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

    public static void Info(string message) =>
        MessageBox.Show(message, "Auto Invoicing", MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static string Money(decimal d) => Core.Money.Format(d);

    public static string Qty(decimal d) => d.ToString("0.###", CultureInfo.CurrentCulture);

    public static bool TryParseDecimal(string text, out decimal value) =>
        decimal.TryParse(text.Replace("$", "").Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out value);

    public static decimal ParseDecimal(string text, string field)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0m;
        if (!TryParseDecimal(text, out var value))
            throw new ArgumentException($"{field} must be a number.");
        return value;
    }

    public static int? ParseOptionalInt(string text, string field)
    {
        var s = text.Replace(",", "").Trim();
        if (s.Length == 0)
            return null;
        if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.CurrentCulture, out var value))
            throw new ArgumentException($"{field} must be a whole number.");
        return value;
    }

    public static DataGridView Grid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            AutoGenerateColumns = false,
            BackgroundColor = SystemColors.Window,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        };
        grid.DataError += (_, e) => e.ThrowException = false;
        return grid;
    }

    public static DataGridViewTextBoxColumn Col(string header, string property, float weight = 100, string? format = null,
        bool right = false)
    {
        var col = new DataGridViewTextBoxColumn
        {
            HeaderText = header,
            DataPropertyName = property,
            FillWeight = weight,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        };
        if (format != null)
            col.DefaultCellStyle.Format = format;
        if (right)
            col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        return col;
    }

    public static DataGridViewTextBoxColumn MoneyCol(string header, string property, float weight = 60) =>
        Col(header, property, weight, "C2", right: true);

    public static T? Selected<T>(this DataGridView grid) where T : class =>
        grid.CurrentRow?.DataBoundItem as T;

    public static FlowLayoutPanel ButtonBar(DockStyle dock = DockStyle.Top) => new()
    {
        Dock = dock,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Padding = new Padding(4),
        WrapContents = true,
    };

    public static Button Button(string text, EventHandler onClick)
    {
        var b = new Button { Text = text, AutoSize = true, Padding = new Padding(6, 2, 6, 2), Margin = new Padding(3) };
        b.Click += onClick;
        return b;
    }

    /// <summary>Two-column label/control form layout.</summary>
    public static TableLayoutPanel FieldTable()
    {
        var t = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = true,
            Padding = new Padding(8),
        };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return t;
    }

    public static T AddField<T>(this TableLayoutPanel t, string label, T control, int height = 0) where T : Control
    {
        var row = t.RowCount++;
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 8, 3) }, 0, row);
        control.Dock = DockStyle.Fill;
        if (height > 0)
            control.Height = height;
        t.Controls.Add(control, 1, row);
        return control;
    }

    public static TextBox Text(string value = "", bool multiline = false) => new()
    {
        Text = value,
        Multiline = multiline,
        ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None,
        AcceptsReturn = multiline,
    };

    /// <summary>OK/Cancel strip for dialogs. The OK handler returns false to keep the dialog open.</summary>
    public static FlowLayoutPanel DialogButtons(Form form, Func<bool> onOk, string okText = "Save")
    {
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(6) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var ok = new Button { Text = okText, AutoSize = true };
        ok.Click += (_, _) =>
        {
            var success = false;
            Try(() => success = onOk());
            if (success)
            {
                form.DialogResult = DialogResult.OK;
                form.Close();
            }
        };
        bar.Controls.Add(cancel);
        bar.Controls.Add(ok);
        form.AcceptButton = ok;
        form.CancelButton = cancel;
        return bar;
    }

    public static void SetupDialog(Form f, string title, int width, int height)
    {
        f.Text = title;
        f.StartPosition = FormStartPosition.CenterParent;
        f.ClientSize = new Size(width, height);
        f.MinimizeBox = false;
        f.ShowInTaskbar = false;
        f.Font = new Font("Segoe UI", 9.5f);
    }

    /// <summary>
    /// SplitContainer whose splitter starts at a fraction of its width. Setting SplitterDistance
    /// before the control has its real size throws, so it is applied on the first real resize.
    /// </summary>
    public static SplitContainer Split(float fraction, DockStyle dock = DockStyle.Fill)
    {
        var split = new SplitContainer { Dock = dock };
        var applied = false;
        split.SizeChanged += (_, _) =>
        {
            if (applied || split.Width < 300)
                return;
            applied = true;
            split.SplitterDistance = (int)(split.Width * fraction);
        };
        return split;
    }

    public static BindingList<T> Bind<T>(this DataGridView grid, IEnumerable<T> items)
    {
        var list = new BindingList<T>(items.ToList());
        grid.DataSource = list;
        return list;
    }
}
