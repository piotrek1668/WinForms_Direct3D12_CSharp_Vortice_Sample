using System.ComponentModel;

#nullable disable

namespace DirectX3D12Example
{
    public partial class MainWindow : Form
    {
        private D3D12GraphicsDevice graphicDevice;
        private Label label;
        private Control leftControl;
        private Control rightControl;
        private Button button;
        private System.Windows.Forms.Timer timer;
        private bool rendering = true;

        public MainWindow()
        {
            InitializeAdditionalControls();
            InitializeComponent();

            this.graphicDevice = new D3D12GraphicsDevice(this, this.leftControl, this.rightControl);
            this.graphicDevice.OnInit();
        }

        private void Button_Click(object sender, EventArgs e)
        {
            if (rendering)
            {
                rendering = false;
                timer?.Stop();
            }
            else
            {
                rendering = true;
                timer?.Start();
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
            button = new Button
            {
                Location = new Point(10, 10),
                Size = new Size(120, 40),
                Text = "Toggle rendering"
            };

            label = new Label
            {
                Location = new Point(button.Location.X + button.Size.Width + 10, (button.Height / 2)),
                Size = new Size(500, 20),
                Text = "Click to draw"
            };

            leftControl = new Control
            {
                Location = new Point(10, button.Location.Y + button.Size.Height + 10),
                Size = new Size(600, 500),
                BackColor = Color.Gray
            };

            rightControl = new Control
            {
                Location = new Point(leftControl.Location.X + leftControl.Size.Width + 10, button.Location.Y + button.Size.Height + 10),
                Size = new Size(600, 500),
                BackColor = Color.Gray
            };

            Label leftLabel = new()
            {
                Location = new Point(leftControl.Location.X + (rightControl.Size.Width / 2) - 50, leftControl.Location.Y + leftControl.Height),
                Size = new Size(100, 40),
                Text = "Direct3D12"
            };

            Label rightLabel = new()
            {
                Location = new Point(rightControl.Location.X + (rightControl.Size.Width / 2) - 50, rightControl.Location.Y + rightControl.Height),
                Size = new Size(100, 40),
                Text = "Direct2D1 DirectWrite"
            };

            button.Click += Button_Click;

            this.Controls.Add(button);
            this.Controls.Add(label);
            this.Controls.Add(leftControl);
            this.Controls.Add(rightControl);
            this.Controls.Add(leftLabel);
            this.Controls.Add(rightLabel);

            timer = new System.Windows.Forms.Timer();
            timer.Tick += Timer_Tick;
            timer.Interval = 20;
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (this.graphicDevice != null && this.graphicDevice.Initialized)
            {
                graphicDevice?.OnUpdate();
                graphicDevice?.OnRender();
                graphicDevice?.OnRender2D();
            }
        }

        public void UpdateLabelText(string text)
        {
            this.label!.Text = text;
        }
    }
}