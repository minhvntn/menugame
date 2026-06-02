using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace GameUpdater.WinForms.Forms
{
    public class TargetDriveSelectionForm : Form
    {
        public string SelectedDrive { get; private set; } = string.Empty;

        private readonly IReadOnlyList<string> _drives;
        private readonly FlowLayoutPanel _radioPanel;

        public TargetDriveSelectionForm(IReadOnlyList<string> drives)
        {
            _drives = drives;

            Text = "Chọn đích cài đặt máy chủ";
            Width = 400;
            Height = 300;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.White;
            ShowIcon = false;

            var titleLabel = new Label
            {
                Text = "Chọn ổ đĩa để tải trò chơi mới:",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, 20)
            };
            Controls.Add(titleLabel);

            _radioPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Location = new Point(20, 50),
                Size = new Size(340, 150),
                AutoScroll = true
            };
            Controls.Add(_radioPanel);

            PopulateDrives();

            var okButton = new Button
            {
                Text = "Xác nhận",
                DialogResult = DialogResult.OK,
                Location = new Point(200, 220),
                Size = new Size(80, 30),
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            okButton.FlatAppearance.BorderSize = 0;

            var cancelButton = new Button
            {
                Text = "Hủy bỏ",
                DialogResult = DialogResult.Cancel,
                Location = new Point(290, 220),
                Size = new Size(80, 30),
                BackColor = Color.FromArgb(241, 245, 249),
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat
            };
            cancelButton.FlatAppearance.BorderSize = 0;

            Controls.Add(okButton);
            Controls.Add(cancelButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        private void PopulateDrives()
        {
            bool isFirst = true;
            foreach (var drive in _drives)
            {
                string infoText = drive;
                try
                {
                    var driveRoot = Path.GetPathRoot(Path.GetFullPath(drive));
                    if (driveRoot != null)
                    {
                        var di = new DriveInfo(driveRoot);
                        if (di.IsReady)
                        {
                            double freeGb = di.AvailableFreeSpace / 1024d / 1024d / 1024d;
                            infoText = $"{drive} (Trống: {freeGb:0.0} GB)";
                        }
                    }
                }
                catch { }

                var rb = new RadioButton
                {
                    Text = infoText,
                    Tag = drive,
                    AutoSize = true,
                    Margin = new Padding(0, 0, 0, 10),
                    Font = new Font("Segoe UI", 9.5f),
                    Checked = isFirst
                };
                
                rb.CheckedChanged += (s, e) =>
                {
                    if (rb.Checked) SelectedDrive = drive;
                };

                if (isFirst)
                {
                    SelectedDrive = drive;
                    isFirst = false;
                }

                _radioPanel.Controls.Add(rb);
            }
        }
    }
}
