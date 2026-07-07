using System.Collections.Generic;
using System.Windows.Forms;
using ACTLogsUploader.Config;

namespace ACTLogsUploader.UI
{
    internal sealed class FightPickerForm : Form
    {
        private readonly CheckedListBox _list;

        public FightPickerForm(IList<string> fightLabels)
        {
            Text = Loc.T("fp.title");
            Width = 460;
            Height = 460;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;

            _list = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true };
            foreach (var label in fightLabels) _list.Items.Add(label, true);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 40, Padding = new Padding(6) };
            var ok = new Button { Text = Loc.T("fp.upload"), DialogResult = DialogResult.OK, AutoSize = true };
            var cancel = new Button { Text = Loc.T("fp.cancel"), DialogResult = DialogResult.Cancel, AutoSize = true };
            var all = new Button { Text = Loc.T("fp.all"), AutoSize = true };
            var none = new Button { Text = Loc.T("fp.none"), AutoSize = true };
            all.Click += (s, e) => SetAll(true);
            none.Click += (s, e) => SetAll(false);
            buttons.Controls.AddRange(new Control[] { ok, cancel, none, all });

            Controls.Add(_list);
            Controls.Add(buttons);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        private void SetAll(bool value)
        {
            for (int i = 0; i < _list.Items.Count; i++) _list.SetItemChecked(i, value);
        }

        public List<int> SelectedIndices()
        {
            var result = new List<int>();
            foreach (int i in _list.CheckedIndices) result.Add(i);
            return result;
        }
    }
}
