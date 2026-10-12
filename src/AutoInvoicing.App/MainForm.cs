using AutoInvoicing.App.Forms;
using AutoInvoicing.App.Views;

namespace AutoInvoicing.App;

internal sealed class MainForm : Form
{
    public MainForm(AppServices app, string dbPath)
    {
        Text = $"{app.LoadSettings().ShopName} - Auto Invoicing";
        Size = new Size(1280, 820);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);

        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(14, 6) };
        var views = new IRefreshable[]
        {
            new JobTicketsView(app), new InvoicesView(app), new CustomersView(app), new InventoryView(app), new ReportsView(app),
        };
        foreach (var v in views)
        {
            var page = new TabPage(v.Title);
            var control = (Control)v;
            control.Dock = DockStyle.Fill;
            page.Controls.Add(control);
            tabs.TabPages.Add(page);
        }
        tabs.SelectedIndexChanged += (_, _) => views[tabs.SelectedIndex].RefreshData();

        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("&File");
        file.DropDownItems.Add("Shop &Settings...", null, (_, _) =>
        {
            using var f = new SettingsForm(app, dbPath);
            if (f.ShowDialog(this) == DialogResult.OK)
                Text = $"{app.LoadSettings().ShopName} - Auto Invoicing";
        });
        file.DropDownItems.Add("&Back Up Database...", null, (_, _) => Backup(app, dbPath));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("E&xit", null, (_, _) => Close());
        menu.Items.Add(file);
        MainMenuStrip = menu;

        Controls.Add(tabs);
        Controls.Add(menu);
        Shown += (_, _) =>
        {
            views[0].RefreshData();
            if (app.LoadSettings().TaxRatePercent == 0m)
                Ui.Info("Welcome! Open File > Shop Settings to enter your shop name, address and sales tax rate before printing invoices.");
        };
    }

    private void Backup(AppServices app, string dbPath)
    {
        using var dlg = new SaveFileDialog
        {
            FileName = $"shop-backup-{DateTime.Now:yyyyMMdd-HHmm}.db",
            Filter = "Database backup (*.db)|*.db",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK)
            return;
        Ui.Try(() =>
        {
            // VACUUM INTO writes a consistent copy even while the app has the database open.
            using var conn = app.Db.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "VACUUM INTO $path";
            cmd.Parameters.AddWithValue("$path", dlg.FileName);
            if (File.Exists(dlg.FileName))
                File.Delete(dlg.FileName);
            cmd.ExecuteNonQuery();
            Ui.Info("Backup saved to " + dlg.FileName);
        });
    }
}
