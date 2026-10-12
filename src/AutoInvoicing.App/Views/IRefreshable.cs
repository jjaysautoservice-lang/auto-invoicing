namespace AutoInvoicing.App.Views;

internal interface IRefreshable
{
    string Title { get; }
    void RefreshData();
}
