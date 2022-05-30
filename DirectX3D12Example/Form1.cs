using System.ComponentModel;

namespace DirectX3D12Example
{
    public partial class Form1 : Form
    {
        private D3D12GraphicsDevice? graphicDevice;   
        private Label? label;
        private Control? control1;
        private Control? control2;
        private CheckBox? checkBox;
        private Button? button;

        public Form1()
        {
            InitializeAdditionalControls();
            InitializeComponent();

            Control[] controls = { this, this.control1!, this.control2! };
            this.graphicDevice = new D3D12GraphicsDevice(controls);
        }

        private void Button_Click(object? sender, EventArgs e)
        {
            this.graphicDevice!.OnInit();

            button!.Enabled = false;
            checkBox!.Enabled = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (this.graphicDevice != null && this.graphicDevice.initialized)
            {
                this.graphicDevice.OnUpdate();
                this.graphicDevice.OnRender();
            }
            
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            this.graphicDevice?.Dispose();
            this.graphicDevice = null;
        }

        private void InitializeAdditionalControls()
        {
            button = new Button();
            button.Location = new Point(10, 10);
            button.Size = new Size(120, 40);
            button.Text = "Draw";
            button.Click += Button_Click;
            this.Controls.Add(button);

            label = new Label();
            label.Location = new Point(button.Location.X + button.Size.Width + 10, (button.Height/2));
            label.Size = new Size(300, 20);
            label.Text = "Click to draw";
            this.Controls.Add(label);

            control1 = new Control();
            control1.Location = new Point(10, button.Location.Y + button.Size.Height + 10);
            control1.Size = new Size(500, 400);
            control1.BackColor = Color.Gray;
            this.Controls.Add(control1);

            control2 = new Control();
            control2.Location = new Point(control1.Location.X + control1.Size.Width + 10, button.Location.Y + button.Size.Height + 10);
            control2.Size = new Size(500, 400);
            control2.BackColor = Color.Gray;
            this.Controls.Add(control2);

            checkBox = new CheckBox();
            checkBox.Location = new Point(control2.Location.X, (button.Height/2));
            checkBox.CheckStateChanged += CheckBox_CheckStateChanged;
            checkBox.Size = new Size(200, 20);
            checkBox.Text = "Use WARP device?";
            this.Controls.Add(checkBox);

            Label leftLabel = new Label();
            leftLabel.Location = new Point(control1.Location.X + (control2.Size.Width/2) - 50, control1.Location.Y + control1.Height);
            leftLabel.Size = new Size(100, 40);
            leftLabel.Text = "Front buffer";
            this.Controls.Add(leftLabel);

            Label rightLabel = new Label();
            rightLabel.Location = new Point(control2.Location.X + (control2.Size.Width/2) - 50, control2.Location.Y + control2.Height);
            rightLabel.Size = new Size(100, 40);
            rightLabel.Text = "Back buffer";
            this.Controls.Add(rightLabel);
        }

        private void CheckBox_CheckStateChanged(object? sender, EventArgs e)
        {
            if (sender is CheckBox checkBox)
            {
                this.graphicDevice?.ChangeDevice(checkBox.Checked);
            }
        }

        public void UpdateLabelText(string text)
        {
            this.label!.Text = text;
        }
    }
}