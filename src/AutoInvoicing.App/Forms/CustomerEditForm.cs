using AutoInvoicing.Core.Models;

namespace AutoInvoicing.App.Forms;

internal sealed class CustomerEditForm : Form
{
    public Customer Customer { get; }

    public CustomerEditForm(AppServices app, Customer customer)
    {
        Customer = customer;
        Ui.SetupDialog(this, customer.Id == 0 ? "New Customer" : "Edit Customer", 480, 360);
        var t = Ui.FieldTable();
        var name = t.AddField("Name", Ui.Text(customer.Name));
        var phone = t.AddField("Phone", Ui.Text(customer.Phone));
        var email = t.AddField("Email", Ui.Text(customer.Email));
        var address = t.AddField("Address", Ui.Text(customer.Address, multiline: true), 60);
        var notes = t.AddField("Notes", Ui.Text(customer.Notes, multiline: true), 60);
        Controls.Add(t);
        Controls.Add(Ui.DialogButtons(this, () =>
        {
            customer.Name = name.Text.Trim();
            customer.Phone = phone.Text.Trim();
            customer.Email = email.Text.Trim();
            customer.Address = address.Text.Trim();
            customer.Notes = notes.Text.Trim();
            app.Customers.Save(customer);
            return true;
        }));
    }
}
