using AutoInvoicing.Core.Models;

namespace AutoInvoicing.App.Forms;

/// <summary>
/// Customer card: blue title bar, name fields, grouped Address and Phone Numbers
/// sections, then email, note and the Save / Close buttons. Laid out at 96 DPI and
/// scaled by Windows for other DPI settings.
/// </summary>
internal sealed class CustomerEditForm : Form
{
    private const int PhoneRows = 3;
    private const int Pad = 16;
    private const int ContentWidth = 628;

    private static readonly Font LabelFont = new("Segoe UI", 10f);
    private static readonly Font InputFont = new("Segoe UI", 10.5f);

    private readonly AppServices _app;
    private readonly InputBox _last;
    private readonly InputBox _first;
    private readonly InputBox _address1;
    private readonly InputBox _address2;
    private readonly InputBox _city;
    private readonly InputBox _state;
    private readonly InputBox _zip;
    private readonly (TallComboBox Type, InputBox Number, InputBox Note)[] _phones = new (TallComboBox, InputBox, InputBox)[PhoneRows];
    private readonly InputBox _email;
    private readonly InputBox _notes;
    private int _tabIndex;

    public Customer Customer { get; }

    public CustomerEditForm(AppServices app, Customer customer)
    {
        _app = app;
        Customer = customer;

        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = customer.Id == 0 ? "New Customer" : "Edit Customer";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        BackColor = Color.White;
        Font = InputFont;
        ClientSize = new Size(Pad * 2 + ContentWidth, 802);

        Controls.Add(Header(customer.Id == 0 ? "Adding a new customer" : $"Editing {customer.Name}"));

        // Name
        AddLabel(this, "Last name / company", Pad, 92);
        AddLabel(this, "First name", 356, 92);
        _last = AddInput(this, customer.LastName, Pad, 114, 324, required: true);
        _last.Placeholder = "Required input";
        _first = AddInput(this, customer.FirstName, 356, 114, 288);

        // Address
        var address = AddSection("Address", 160, 222);
        AddLabel(address, "Address 1", 16, 30);
        _address1 = AddInput(address, customer.Address1, 16, 50, 596);
        AddLabel(address, "Address 2", 16, 92);
        _address2 = AddInput(address, customer.Address2, 16, 112, 596);
        AddLabel(address, "City", 16, 154);
        AddLabel(address, "State", 252, 154);
        AddLabel(address, "Zip code", 352, 154);
        _city = AddInput(address, customer.City, 16, 174, 224);
        _state = AddInput(address, customer.State, 252, 174, 88, maxLength: 2);
        _state.Box.CharacterCasing = CharacterCasing.Upper;
        _zip = AddInput(address, customer.Zip, 352, 174, 260, maxLength: 10);

        // Phone numbers
        var phones = AddSection("Phone numbers", 396, 188);
        AddLabel(phones, "Type", 16, 30);
        AddLabel(phones, "Number", 188, 30);
        AddLabel(phones, "Note", 380, 30);
        for (var i = 0; i < PhoneRows; i++)
        {
            var phone = i < customer.Phones.Count ? customer.Phones[i] : new CustomerPhone();
            var y = 52 + i * 44;
            _phones[i] = (AddPhoneType(phones, phone.Type, 16, y, 160),
                AddInput(phones, phone.Number, 188, y, 180),
                AddInput(phones, phone.Note, 380, y, 232));
        }

        // Email and note
        AddLabel(this, "Email", Pad, 598);
        _email = AddInput(this, customer.Email, Pad, 620, ContentWidth);
        AddLabel(this, "Note", Pad, 666);
        _notes = AddInput(this, customer.Notes, Pad, 688, ContentWidth);

        var save = Styled.Button("Save", Styled.SaveBack, Color.White, Styled.SaveBack);
        save.Bounds = new Rectangle(Pad, 738, 300, 48);
        save.TabIndex = _tabIndex++;
        save.Click += (_, _) => Save();
        var close = Styled.Button("Close", Styled.SectionBack, Color.Black, Styled.SectionBorder);
        close.Bounds = new Rectangle(Pad + ContentWidth - 300, 738, 300, 48);
        close.TabIndex = _tabIndex++;
        close.DialogResult = DialogResult.Cancel;
        Controls.Add(save);
        Controls.Add(close);
        AcceptButton = save;
        CancelButton = close;

        Shown += (_, _) => _last.Box.Focus();
    }

    // A borderless window still gets the standard drop shadow.
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= 0x20000; // CS_DROPSHADOW
            return cp;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Styled.Border);
        e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
    }

    private void Save()
    {
        var phones = _phones
            .Select(p => new CustomerPhone { Type = p.Type.Text, Number = p.Number.Text, Note = p.Note.Text })
            .Concat(Customer.Phones.Skip(PhoneRows)) // keep any extra numbers this form has no row for
            .ToList();
        var edited = new Customer
        {
            Id = Customer.Id,
            LastName = _last.Text, FirstName = _first.Text,
            Address1 = _address1.Text, Address2 = _address2.Text, City = _city.Text, State = _state.Text, Zip = _zip.Text,
            Phones = phones, Email = _email.Text, Notes = _notes.Text,
        };
        if (!Ui.Try(() => _app.Customers.Save(edited)))
        {
            _last.Box.Focus();
            return;
        }

        Customer.Id = edited.Id;
        Customer.Name = edited.Name;
        Customer.LastName = edited.LastName;
        Customer.FirstName = edited.FirstName;
        Customer.Phone = edited.Phone;
        Customer.Phones = edited.Phones;
        Customer.Email = edited.Email;
        Customer.Address = edited.Address;
        Customer.Address1 = edited.Address1;
        Customer.Address2 = edited.Address2;
        Customer.City = edited.City;
        Customer.State = edited.State;
        Customer.Zip = edited.Zip;
        Customer.Notes = edited.Notes;
        DialogResult = DialogResult.OK;
        Close();
    }

    private Panel Header(string title)
    {
        var header = new Panel { Bounds = new Rectangle(0, 0, ClientSize.Width, 80), BackColor = Styled.HeaderBack };
        header.Controls.Add(Styled.Caption("CUSTOMER INFORMATION", 18, 12, new Font("Segoe UI", 10.5f), Styled.HeaderCaption));
        header.Controls.Add(Styled.Caption(title, 18, 36, new Font("Segoe UI", 14f, FontStyle.Bold), Color.White));
        var close = new Label
        {
            Text = "✕",
            Font = new Font("Segoe UI", 11f),
            ForeColor = Styled.HeaderCaption,
            AutoSize = true,
            Location = new Point(ClientSize.Width - 34, 8),
            Cursor = Cursors.Hand,
        };
        close.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        close.MouseEnter += (_, _) => close.ForeColor = Color.White;
        close.MouseLeave += (_, _) => close.ForeColor = Styled.HeaderCaption;
        header.Controls.Add(close);
        header.DragsForm(this);
        foreach (Control c in header.Controls)
            if (c != close)
                c.DragsForm(this);
        return header;
    }

    private SectionPanel AddSection(string caption, int y, int height)
    {
        var section = new SectionPanel(caption) { Bounds = new Rectangle(Pad, y, ContentWidth, height), TabIndex = _tabIndex++ };
        Controls.Add(section);
        return section;
    }

    private static void AddLabel(Control parent, string text, int x, int y) =>
        parent.Controls.Add(Styled.Caption(text, x - 1, y, LabelFont));

    private InputBox AddInput(Control parent, string value, int x, int y, int width, bool required = false, int maxLength = 0)
    {
        var input = new InputBox(value, maxLength)
        {
            Required = required,
            Bounds = new Rectangle(x, y, width, 34),
            TabIndex = _tabIndex++,
        };
        parent.Controls.Add(input);
        return input;
    }

    private TallComboBox AddPhoneType(Control parent, string value, int x, int y, int width)
    {
        var combo = new TallComboBox { Location = new Point(x, y), Width = width, TabIndex = _tabIndex++ };
        combo.Items.Add("");
        combo.Items.AddRange(CustomerPhone.Types);
        if (value.Length > 0 && !combo.Items.Contains(value))
            combo.Items.Add(value);
        combo.SelectedItem = value;
        parent.Controls.Add(combo);
        return combo;
    }
}
