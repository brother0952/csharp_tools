using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Newtonsoft.Json;
using System.Windows.Documents;
using System.Windows.Media;

namespace KimiChat
{
    public partial class MainWindow : Window
    {
        private readonly string apiEndpoint = "https://api.moonshot.cn/v1/chat/completions";
        private readonly ConfigManager configManager;

        public MainWindow()
        {
            InitializeComponent();
            configManager = new ConfigManager();

            // 测试API Key是否已配置
            string apiKey = configManager.GetApiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                MessageBox.Show("警告：API Key未配置，请检查config.json文件", "配置提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            await SendMessage();
        }

        private void TxtMessage_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    // Ctrl + Enter: 插入换行
                    int caretIndex = txtMessage.CaretIndex;
                    txtMessage.Text = txtMessage.Text.Insert(caretIndex, Environment.NewLine);
                    txtMessage.CaretIndex = caretIndex + Environment.NewLine.Length;
                    e.Handled = true;
                }
                else
                {
                    // 直接回车: 发送消息
                    e.Handled = true;
                    if (!string.IsNullOrWhiteSpace(txtMessage.Text))
                    {
                        _ = SendMessage();
                    }
                }
            }
        }

        private async Task SendMessage()
        {
            if (string.IsNullOrWhiteSpace(txtMessage.Text))
                return;

            string apiKey = configManager.GetApiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                MessageBox.Show("请先在config.json中设置API Key");
                return;
            }

            string messageText = txtMessage.Text;  // 保存消息内容
            txtMessage.Clear();  // 立即清空输入框
            btnSend.IsEnabled = false;

            try
            {
                // 添加用户消息（右对齐）
                var userRun = new Run(messageText)
                {
                    Foreground = (SolidColorBrush)FindResource("UserMessageColor")
                };
                var userPara = new Paragraph(userRun)
                {
                    TextAlignment = TextAlignment.Right,
                    Margin = new Thickness(50, 5, 0, 5)
                };
                txtResponse.Document.Blocks.Add(userPara);

                // 创建Kimi回复的段落
                var kimiPara = new Paragraph()
                {
                    TextAlignment = TextAlignment.Left,
                    Margin = new Thickness(0, 5, 50, 5)
                };
                txtResponse.Document.Blocks.Add(kimiPara);
                
                // 发送消息并获取回复
                string response = await SendMessageToKimi(messageText, apiKey);
                
                // 按行分割回复
                var lines = response.Split(new[] { "\n", "\r\n" }, StringSplitOptions.None);
                
                // 逐行显示回复
                foreach (var line in lines)
                {
                    if (kimiPara.Inlines.Count > 0)
                    {
                        // 添加换行
                        kimiPara.Inlines.Add(new LineBreak());
                    }

                    var kimiRun = new Run(line)
                    {
                        Foreground = (SolidColorBrush)FindResource("KimiMessageColor")
                    };
                    kimiPara.Inlines.Add(kimiRun);
                    
                    txtResponse.ScrollToEnd();
                    await Task.Delay(50); // 每行显示间隔50毫秒
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发送消息时出错: {ex.Message}");
            }
            finally
            {
                btnSend.IsEnabled = true;
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
                    throw new Exception($"API��求失败: {responseString}");
                }

                dynamic responseObj = JsonConvert.DeserializeObject(responseString);
                return responseObj.choices[0].message.content;
            }
        }
    }
} 