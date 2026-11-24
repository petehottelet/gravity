using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Gravity
{
    internal sealed class FormEditor : Form
    {
        private readonly List<Func<PdfFormEdit>> readers = new List<Func<PdfFormEdit>>();
        public List<PdfFormEdit> Edits { get { return readers.Select(r => r()).Where(e => e != null).ToList(); } }

        public FormEditor(IList<PdfFormField> fields, int page, int selected)
        {
            Text = "Fill form — page " + (page + 1);
            Font = new Font("Segoe UI", 9f);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            ClientSize = new Size(520, 480);
            MinimumSize = new Size(400, 320);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = MaximizeBox = false;
            ShowInTaskbar = false;

            var body = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(18) };
            var rows = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new Padding(0, 0, 18, 0) };
            rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.Controls.Add(rows);
            var hint = new Label { Dock = DockStyle.Top, Height = 48, Padding = new Padding(18, 12, 18, 0), Text = "Fill the fields on this page, then apply your changes. Save the PDF to keep them." };
            var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54, Padding = new Padding(12), FlowDirection = FlowDirection.RightToLeft };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            var apply = new Button { Text = "Apply", DialogResult = DialogResult.OK, AutoSize = true };
            footer.Controls.AddRange(new Control[] { cancel, apply });
            Controls.Add(body); Controls.Add(hint); Controls.Add(footer);
            AcceptButton = apply; CancelButton = cancel;
            Control focus = null;
            var radioGroups = new Dictionary<string, List<RadioButton>>();
            foreach (var field in fields)
            {
                var f = field;
                string label = f.Label + (f.ReadOnly ? " (read only)" : "") + ((f.Flags & 2) != 0 ? " (required)" : "");
                rows.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 12, 0, 5), UseMnemonic = false });
                Control control;
                if (f.Type == 2 || f.Type == 3)
                {
                    if (f.Type == 2)
                    {
                        var box = new CheckBox { Text = "Checked", Checked = f.Checked, AutoSize = true };
                        control = box;
                        readers.Add(() => box.Checked == f.Checked ? null : new PdfFormEdit { Index = f.Index, Checked = box.Checked });
                    }
                    else
                    {
                        var radio = new RadioButton { Text = string.IsNullOrEmpty(f.ExportValue) ? "Select" : f.ExportValue, Checked = f.Checked, AutoSize = true, AutoCheck = false };
                        control = radio;
                        List<RadioButton> group;
                        if (!radioGroups.TryGetValue(f.Name, out group)) radioGroups[f.Name] = group = new List<RadioButton>();
                        group.Add(radio);
                        radio.Click += delegate { foreach (var other in group) other.Checked = other == radio; };
                        readers.Add(() => !radio.Checked || f.Checked ? null : new PdfFormEdit { Index = f.Index, Checked = true });
                    }
                }
                else if (f.Type == 4 || f.Type == 5)
                {
                    if (f.Multiple)
                    {
                        var list = new CheckedListBox { Height = 100, CheckOnClick = true, IntegralHeight = false };
                        foreach (var option in f.Options) list.Items.Add(option);
                        foreach (int i in f.Selected) list.SetItemChecked(i, true);
                        control = list;
                        readers.Add(() => { var indices = list.CheckedIndices.Cast<int>().ToArray(); return indices.SequenceEqual(f.Selected) ? null : new PdfFormEdit { Index = f.Index, Selected = indices }; });
                    }
                    else
                    {
                        var combo = new ComboBox { DropDownStyle = f.EditableChoice ? ComboBoxStyle.DropDown : ComboBoxStyle.DropDownList };
                        combo.Items.AddRange(f.Options.Cast<object>().ToArray());
                        if (f.Selected.Count > 0) combo.SelectedIndex = f.Selected[0];
                        else if (f.EditableChoice) combo.Text = f.Value;
                        control = combo;
                        readers.Add(() => { var indices = combo.SelectedIndex < 0 ? new int[0] : new[] { combo.SelectedIndex }; return indices.SequenceEqual(f.Selected) && (!f.EditableChoice || combo.Text == f.Value) ? null : new PdfFormEdit { Index = f.Index, Text = combo.Text, Selected = indices }; });
                    }
                }
                else if (f.Type == 6)
                {
                    var text = new TextBox { Text = f.Value, Multiline = f.Multiline, Height = f.Multiline ? 84 : 24, ScrollBars = f.Multiline ? ScrollBars.Vertical : ScrollBars.None, UseSystemPasswordChar = f.Password, AcceptsReturn = f.Multiline, MaxLength = 0 };
                    control = text;
                    readers.Add(() => text.Text == f.Value ? null : new PdfFormEdit { Index = f.Index, Text = text.Text });
                }
                else control = new Label { Text = f.Type == 7 ? "Digital signatures cannot be filled here." : "This field type is not supported.", AutoSize = true };
                control.Enabled = f.Supported && !f.ReadOnly;
                control.AccessibleName = label;
                control.Dock = DockStyle.Top;
                control.Margin = new Padding(0, 0, 0, 4);
                rows.Controls.Add(control);
                if (control.Enabled && (focus == null || f.Index == selected)) focus = control;
            }
            apply.Enabled = fields.Any(f => f.Supported && !f.ReadOnly);
            Shown += delegate { if (focus != null) { body.ScrollControlIntoView(focus); focus.Focus(); } };
        }
    }
}
