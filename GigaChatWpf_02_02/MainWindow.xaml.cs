using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Media;
using System.Runtime.InteropServices;

namespace GigaChatWpf_02_02
{
    public partial class MainWindow : Window
    {
        static string ClientId = "0199d470-bb93-7ce2-b0df-620ead27395d";
        static string AuthorizationKey = "MDE5OWQ0NzAtYmI5My03Y2UyLWIwZGYtNjIwZWFkMjczOTVkOjZkYWIzODE4LTgyZDQtNGMwZS05NDRjLTQ0MzY1NWVjODg1YQ==";

        private string token;
        private HttpClient client;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await InitApp();
        }

        private async Task InitApp()
        {
            try
            {
                StatusTextBlock.Text = "Подключаемся...";
                ProgressBar.IsIndeterminate = true;

                token = await GetToken();
                if (token == null)
                {
                    MessageBox.Show("Не получилось подключиться. Проверьте интернет и данные для входа.");
                    return;
                }

                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
                };

                client = new HttpClient(handler);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                client.DefaultRequestHeaders.Add("X-Client-ID", ClientId);

                StatusTextBlock.Text = "Готово";
                ProgressBar.IsIndeterminate = false;
                GenerateButton.IsEnabled = true;
                HolidayButton.IsEnabled = true;

                PromptTextBox.TextChanged += (s, ev) => UpdatePreview();
                StyleComboBox.SelectionChanged += (s, ev) => UpdatePreview();
                ColorComboBox.SelectionChanged += (s, ev) => UpdatePreview();

                Ratio16_9.Checked += (s, ev) => UpdatePreview();
                Ratio4_3.Checked += (s, ev) => UpdatePreview();
                Ratio1_1.Checked += (s, ev) => UpdatePreview();
                Ratio9_16.Checked += (s, ev) => UpdatePreview();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }

        private async void HolidayButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (client == null)
                {
                    MessageBox.Show("Сначала подключитесь к серверу");
                    await InitApp();
                    return;
                }
                var today = DateTime.Today;
                var holidayPrompt = GetHolidayPrompt(today);

                if (holidayPrompt == null)
                {
                    MessageBox.Show("Сегодня нет известных праздников");
                    return;
                }

                MessageBox.Show($"Будет создано изображение для: {holidayPrompt}");

                PromptTextBox.Text = holidayPrompt;
                StyleComboBox.SelectedIndex = 1; 
                ColorComboBox.SelectedIndex = 0; 
                await GenerateImage();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }

        private string GetHolidayPrompt(DateTime date)
        {
            if (date.Month == 1 && date.Day == 1)
                return "Новогодняя ёлка с игрушками и снегом, праздничное настроение";

            if (date.Month == 2 && date.Day == 14)
                return "День влюбленных, сердечки, розы, романтичная атмосфера";

            if (date.Month == 2 && date.Day == 23)
                return "День защитника Отечества, военная техника, флаги";

            if (date.Month == 3 && date.Day == 8)
                return "Международный женский день, цветы, подарки, весна";

            if (date.Month == 5 && date.Day == 9)
                return "День Победы, салют, георгиевская лента, праздник";

            if (date.Month == 6 && date.Day == 1)
                return "День защиты детей, игрушки, радость, детские игры";

            if (date.Month == 12 && date.Day >= 25 && date.Day <= 31)
                return "Новогодняя атмосфера, снежинки, подарки, Дед Мороз";

            if (date.Day == 13 && date.DayOfWeek == DayOfWeek.Friday)
                return "Мистическая атмосфера пятницы 13-го, луна, тыквы, загадочность";

            if (date.DayOfWeek == DayOfWeek.Sunday)
                return "Расслабляющая воскресная атмосфера, отдых, спокойствие";

            return null;
        }

        private async Task<string> GetToken()
        {
            try
            {
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
                };

                using (var http = new HttpClient(handler))
                {
                    var request = new HttpRequestMessage(HttpMethod.Post,
                        "https://ngw.devices.sberbank.ru:9443/api/v2/oauth");

                    request.Headers.Add("Accept", "application/json");
                    request.Headers.Add("RqUID", ClientId);
                    request.Headers.Add("Authorization", $"Bearer {AuthorizationKey}");

                    var data = new Dictionary<string, string>
                    {
                        { "scope", "GIGACHAT_API_PERS" }
                    };

                    request.Content = new FormUrlEncodedContent(data);

                    var response = await http.SendAsync(request);

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var tokenData = JsonConvert.DeserializeObject<TokenResponse>(json);
                        return tokenData?.access_token;
                    }
                    else
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        MessageBox.Show($"Ошибка получения токена: {response.StatusCode}\n{error}");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения: {ex.Message}");
                return null;
            }
        }

        private void UpdatePreview()
        {
            if (string.IsNullOrEmpty(PromptTextBox.Text))
            {
                PreviewTextBlock.Text = "Введите описание изображения";
                return;
            }

            string style = "Не выбран";
            string colors = "Не выбраны";

            if (StyleComboBox.SelectedItem is ComboBoxItem styleItem)
                style = styleItem.Content.ToString();

            if (ColorComboBox.SelectedItem is ComboBoxItem colorItem)
                colors = colorItem.Content.ToString();

            string ratio = "16:9";
            if (Ratio4_3.IsChecked == true) ratio = "4:3";
            else if (Ratio1_1.IsChecked == true) ratio = "1:1";
            else if (Ratio9_16.IsChecked == true) ratio = "9:16";

            var preview = $"Запрос: {PromptTextBox.Text}\n" +
                         $"Стиль: {style}\n" +
                         $"Цвета: {colors}\n" +
                         $"Размер: {ratio}\n" +
                         $"Обои: {(SetWallpaperCheckBox.IsChecked == true ? "Да" : "Нет")}";

            PreviewTextBlock.Text = preview;
        }

        private async void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            await GenerateImage();
        }

        private async Task GenerateImage()
        {
            if (client == null)
            {
                MessageBox.Show("Сначала подключитесь к серверу");
                await InitApp();
                return;
            }

            if (string.IsNullOrWhiteSpace(PromptTextBox.Text))
            {
                MessageBox.Show("Введите описание изображения");
                PromptTextBox.Focus();
                return;
            }

            try
            {
                GenerateButton.IsEnabled = false;
                HolidayButton.IsEnabled = false;
                ProgressBar.IsIndeterminate = true;
                StatusTextBlock.Text = "Генерируем изображение...";

                var fullPrompt = BuildFullPrompt();

                var imagePath = await CreateImage(fullPrompt);

                if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                {
                    StatusTextBlock.Text = "Готово!";

                    if (SetWallpaperCheckBox.IsChecked == true)
                    {
                        SetAsWallpaper(imagePath);
                    }

                }
                else
                {
                    MessageBox.Show("Не удалось создать изображение");
                    StatusTextBlock.Text = "Ошибка";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
                StatusTextBlock.Text = "Ошибка";
            }
            finally
            {
                GenerateButton.IsEnabled = true;
                HolidayButton.IsEnabled = true;
                ProgressBar.IsIndeterminate = false;
            }
        }

        private string BuildFullPrompt()
        {
            var baseText = PromptTextBox.Text;
            string style = "реализм";
            string colors = "яркие цвета";

            if (StyleComboBox.SelectedItem is ComboBoxItem styleItem)
                style = styleItem.Content.ToString();

            if (ColorComboBox.SelectedItem is ComboBoxItem colorItem)
                colors = colorItem.Content.ToString();


            string ratio = "16:9";
            if (Ratio4_3.IsChecked == true) ratio = "4:3";
            else if (Ratio1_1.IsChecked == true) ratio = "1:1";
            else if (Ratio9_16.IsChecked == true) ratio = "9:16";


            return $"Создай изображение: {baseText}\n\n" +
                   $"Стиль: {style}\n" +
                   $"Цветовая гамма: {colors}\n" +
                   $"Соотношение сторон: {ratio}\n";
        }

        private async Task<string> CreateImage(string prompt)
        {
            try
            {
                if (client == null)
                {
                    throw new Exception("Клиент не инициализирован");
                }

                var messages = new[]
                {
                    new { role = "user", content = prompt }
                };

                var requestData = new
                {
                    model = "GigaChat",
                    messages = messages,
                    function_call = "auto"
                };

                var json = JsonConvert.SerializeObject(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(
                    "https://gigachat.devices.sberbank.ru/api/v1/chat/completions",
                    content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorText = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Ошибка сервера: {response.StatusCode}\n{errorText}");
                }

                var responseText = await response.Content.ReadAsStringAsync();
                var data = JObject.Parse(responseText);
                var html = data["choices"]?[0]?["message"]?["content"]?.ToString();

                if (string.IsNullOrEmpty(html))
                {
                    throw new Exception("Не получили изображение от сервера");
                }

                Debug.WriteLine($"Получен HTML: {html}");

                var match = Regex.Match(html, "<img.*?src=\"(.*?)\"", RegexOptions.IgnoreCase);
                if (!match.Success)
                {
                    throw new Exception("Не нашли изображение в ответе сервера");
                }

                var fileId = match.Groups[1].Value;

                var imageUrl = $"https://gigachat.devices.sberbank.ru/api/v1/files/{fileId}/content";
                var imageResponse = await client.GetAsync(imageUrl);

                if (!imageResponse.IsSuccessStatusCode)
                {
                    throw new Exception($"Не смогли скачать изображение: {imageResponse.StatusCode}");
                }

                var imageBytes = await imageResponse.Content.ReadAsByteArrayAsync();
                var fileName = $"image_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "GigaChat");

                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                var fullPath = Path.Combine(folder, fileName);
                await File.WriteAllBytesAsync(fullPath, imageBytes);

                return fullPath;
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка создания: {ex.Message}");
            }
        }

        private void SetAsWallpaper(string imagePath)
        {
            try
            {
                if (!File.Exists(imagePath))
                {
                    MessageBox.Show("Файл изображения не найден");
                    return;
                }

                const int SPI_SETDESKWALLPAPER = 20;
                const int SPIF_UPDATEINIFILE = 0x01;
                const int SPIF_SENDWININICHANGE = 0x02;

                SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, imagePath, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
                MessageBox.Show("Обои установлены!", "Успех");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось установить обои: {ex.Message}", "Ошибка");
            }
        }

        private void OpenFolder(string folderPath)
        {
            try
            {
                if (Directory.Exists(folderPath))
                {
                    Process.Start("explorer.exe", folderPath);
                }
                else
                {
                    MessageBox.Show("Папка не существует");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть папку: {ex.Message}");
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            PromptTextBox.Clear();
            StyleComboBox.SelectedIndex = 0;
            ColorComboBox.SelectedIndex = 0;
            Ratio16_9.IsChecked = true;
            SetWallpaperCheckBox.IsChecked = true;
            UpdatePreview();
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        private class TokenResponse
        {
            public string access_token { get; set; }
        }
    }
}