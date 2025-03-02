using System;
using System.Windows.Forms;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using Newtonsoft.Json;

namespace KimiChat
{
    public partial class KimiChatForm : Form
    {
        private readonly string apiEndpoint = "https://api.moonshot.cn/v1/chat/completions";
        private TextBox txtMessage;
        private TextBox txtResponse;
        private Button btnSend;
        private ConfigManager configManager;

        public KimiChatForm()
        {
            try
            {
                InitializeComponent();
                configManager = new ConfigManager();
                
                // 设置窗口启动位置为屏幕中心
                this.StartPosition = FormStartPosition.CenterScreen;
                
                // 确保窗口可见
                this.Visible = true;
                this.Show();
                
                // 测试API Key是否已配置
                string apiKey = configManager.GetApiKey();
                if (string.IsNullOrEmpty(apiKey))
                {
                    MessageBox.Show("警告：API Key未配置，请检查config.json文件", "配置提示", 
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"窗体初始化错误: {ex.Message}\n\n{ex.StackTrace}", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitializeComponent()
        {
            this.Text = "Kimi Chat";
            this.Size = new System.Drawing.Size(800, 600);

            txtResponse = new TextBox
            {
                Location = new System.Drawing.Point(12, 12),
                Multiline = true,
                Size = new System.Drawing.Size(760, 430),
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true
            };

            txtMessage = new TextBox
            {
                Location = new System.Drawing.Point(12, 450),
                Multiline = true,
                Size = new System.Drawing.Size(660, 100),
                ScrollBars = ScrollBars.Vertical
            };
            // 添加按键处理
            txtMessage.KeyDown += TxtMessage_KeyDown;

            btnSend = new Button
            {
                Location = new System.Drawing.Point(680, 450),
                Size = new System.Drawing.Size(90, 100),
                Text = "发送"
            };
            btnSend.Click += BtnSend_Click;

            this.Controls.AddRange(new Control[] { txtResponse, txtMessage, btnSend });
        }

        private async void BtnSend_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtMessage.Text))
                return;

            string apiKey = configManager.GetApiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                MessageBox.Show("请先在config.json中设置API Key");
                return;
            }

            btnSend.Enabled = false;
            try
            {
                string response = await SendMessageToKimi(txtMessage.Text, apiKey);
                txtResponse.Text += $"\r\n我: {txtMessage.Text}\r\n";
                txtResponse.Text += $"Kimi: {response}\r\n";
                txtMessage.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发送消息时出错: {ex.Message}");
            }
            finally
            {
                btnSend.Enabled = true;
            }
        }

        private async Task<string> SendMessageToKimi(string message, string apiKey)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                var requestBody = new
                {
                    model = "moonshot-v1-8k",
                    messages = new[]
                    {
                        new { role = "user", content = message }
                    }
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(requestBody),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await client.PostAsync(apiEndpoint, content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"API请求失败: {responseString}");
                }

                dynamic responseObj = JsonConvert.DeserializeObject(responseString);
                return responseObj.choices[0].message.content;
            }
        }

        // 添加按键处理方法
        private void TxtMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (e.Control)
                {
                    // Ctrl + Enter: 插入换行
                    int selectionStart = txtMessage.SelectionStart;
                    txtMessage.Text = txtMessage.Text.Insert(selectionStart, Environment.NewLine);
                    txtMessage.SelectionStart = selectionStart + Environment.NewLine.Length;
                    e.Handled = true;
                }
                else
                {
                    // 直接回车: 发送消息
                    e.Handled = true;
                    if (!string.IsNullOrWhiteSpace(txtMessage.Text))
                    {
                        btnSend.PerformClick();
                    }
                }
            }
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                
                // 创建窗体实例并运行
                KimiChatForm mainForm = new KimiChatForm();
                Application.Run(mainForm);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"程序启动错误: {ex.Message}\n\n{ex.StackTrace}", "严重错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
} 