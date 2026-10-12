using AutoInvoicing.Core.Data;

namespace AutoInvoicing.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) => Ui.ShowError(e.Exception);

        // Optional first argument: path to the database file (e.g. on a shared drive).
        var path = args.Length > 0 ? args[0] : Database.DefaultPath();
        var db = new Database(path);
        db.Initialize();

        Application.Run(new MainForm(new AppServices(db), path));
    }
}
