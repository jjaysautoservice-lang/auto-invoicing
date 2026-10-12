using System.ComponentModel;
using AutoInvoicing.Core;
using AutoInvoicing.Core.Models;

namespace AutoInvoicing.App.Forms;

/// <summary>Editable grid of labor, part and fee lines, shared by job tickets and invoices.</summary>
internal sealed class LineItemsEditor : UserControl
{
    private readonly AppServices _app;
    private readonly DataGridView _grid;
    private readonly FlowLayoutPanel _buttons;
    private BindingList<LineItem> _lines = new();
    private bool _readOnly;

    public event EventHandler? LinesChanged;

    public LineItemsEditor(AppServices app)
    {
        _app = app;
        _grid = Ui.Grid();
        _grid.ReadOnly = false;
        _grid.EditMode = DataGridViewEditMode.EditOnEnter;
        _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;

        var type = new DataGridViewComboBoxColumn
        {
            HeaderText = "Type",
            DataPropertyName = nameof(LineItem.Type),
            DataSource = Enum.GetValues<LineType>(),
            ValueType = typeof(LineType),
            FillWeight = 45,
            FlatStyle = FlatStyle.Flat,
        };
        var taxable = new DataGridViewCheckBoxColumn { HeaderText = "Tax", DataPropertyName = nameof(LineItem.Taxable), FillWeight = 25 };
        var total = Ui.MoneyCol("Amount", nameof(LineItem.Total), 55);
        total.ReadOnly = true;
        _grid.Columns.AddRange(
            type,
            Ui.Col("Part #", nameof(LineItem.PartNumber), 60),
            Ui.Col("Description", nameof(LineItem.Description), 200),
            Ui.Col("Qty / Hrs", nameof(LineItem.Quantity), 45, "0.###", right: true),
            Ui.MoneyCol("Unit Price", nameof(LineItem.UnitPrice), 55),
            taxable,
            total);
        _grid.CellValueChanged += (_, _) => OnChanged();
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.CurrentCell is DataGridViewCheckBoxCell or DataGridViewComboBoxCell)
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _grid.DataError += (_, e) =>
        {
            e.ThrowException = false;
            e.Cancel = false;
            MessageBox.Show("Please enter a number.", "Auto Invoicing");
        };

        _buttons = Ui.ButtonBar();
        _buttons.Controls.AddRange(new Control[]
        {
            Ui.Button("Add Labor", (_, _) => AddLabor()),
            Ui.Button("Add Part from Inventory", (_, _) => AddInventoryPart()),
            Ui.Button("Add Other Part", (_, _) => AddLine(LineType.Part, "", 1m, 0m)),
            Ui.Button("Add Fee / Sublet", (_, _) => AddLine(LineType.Fee, "Shop supplies", 1m, 0m)),
            Ui.Button("Remove Line", (_, _) => RemoveLine()),
            Ui.Button("Move Up", (_, _) => MoveLine(-1)),
            Ui.Button("Move Down", (_, _) => MoveLine(1)),
        });

        Controls.Add(_grid);
        Controls.Add(_buttons);
        _grid.DataSource = _lines;
    }

    public List<LineItem> Lines
    {
        get
        {
            _grid.EndEdit();
            return _lines.ToList();
        }
        set
        {
            _lines = new BindingList<LineItem>(value.Select(l => l.Clone()).ToList());
            _lines.ListChanged += (_, _) => LinesChanged?.Invoke(this, EventArgs.Empty);
            _grid.DataSource = _lines;
            OnChanged();
        }
    }

    /// <summary>The lines as currently shown, without committing a cell that is mid-edit (safe inside change events).</summary>
    public List<LineItem> CurrentLines => _lines.ToList();

    public bool ReadOnlyMode
    {
        get => _readOnly;
        set
        {
            _readOnly = value;
            _grid.ReadOnly = value;
            _buttons.Visible = !value;
        }
    }

    private void OnChanged()
    {
        _grid.Invalidate();
        LinesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AddLabor()
    {
        var settings = _app.LoadSettings();
        AddLine(LineType.Labor, "", 1m, settings.LaborRate);
    }

    private void AddLine(LineType type, string description, decimal qty, decimal price, Part? part = null)
    {
        var settings = _app.LoadSettings();
        var line = new LineItem
        {
            Type = type,
            Description = description,
            Quantity = qty,
            UnitPrice = price,
            Taxable = InvoiceCalculator.DefaultTaxable(type, settings, part?.Taxable ?? true),
        };
        if (part != null)
        {
            line.PartId = part.Id;
            line.PartNumber = part.PartNumber;
            line.UnitCost = part.Cost;
        }
        _lines.Add(line);
        _grid.CurrentCell = _grid.Rows[_lines.Count - 1].Cells[line.Description.Length == 0 ? 2 : 3];
        _grid.BeginEdit(true);
        OnChanged();
    }

    private void AddInventoryPart()
    {
        using var picker = new PartPickerForm(_app);
        if (picker.ShowDialog(this) == DialogResult.OK && picker.SelectedPart is { } part)
        {
            if (part.QuantityOnHand < picker.Quantity)
                Ui.Info($"Heads up: only {Ui.Qty(part.QuantityOnHand)} of {part.PartNumber} on hand.");
            AddLine(LineType.Part, part.Description, picker.Quantity, part.Price, part);
        }
    }

    private void RemoveLine()
    {
        if (_grid.CurrentRow?.DataBoundItem is LineItem line)
        {
            _grid.EndEdit();
            _lines.Remove(line);
            OnChanged();
        }
    }

    private void MoveLine(int delta)
    {
        if (_grid.CurrentRow?.DataBoundItem is not LineItem line)
            return;
        _grid.EndEdit();
        var i = _lines.IndexOf(line);
        var j = i + delta;
        if (j < 0 || j >= _lines.Count)
            return;
        _lines.RemoveAt(i);
        _lines.Insert(j, line);
        _grid.CurrentCell = _grid.Rows[j].Cells[2];
        OnChanged();
    }
}
