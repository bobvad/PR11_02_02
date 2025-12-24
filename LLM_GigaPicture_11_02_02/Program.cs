using LLM_GigaPicture_11_02_02.Classes;
using LLM_GigaPicture_11_02_02.Models;
using LLM_GigaPicture_11_02_02.Response;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LLM_GigaPicture_11_02_02
{
    internal class Program
    {
        static string ClientId = "0199d470-bb93-7ce2-b0df-620ead27395d";
        static string AuthorizationKey = "MDE5OWQ0NzAtYmI5My03Y2UyLWIwZGYtNjIwZWFkMjczOTVkOjZkYWIzODE4LTgyZDQtNGMwZS05NDRjLTQ0MzY1NWVjODg1YQ==";
        static string Token = null;

        static async Task Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Для создания изображений, перед запросом введите /img Ваш запрос \n" +
                "Для текстового запроса, введите просто ваш запрос, без /img");

            Token = await GetToken(ClientId, AuthorizationKey);
            if (Token == null)
            {
                Console.WriteLine("Не удалось получить токен");
                return;
            }

            Console.ForegroundColor = ConsoleColor.White;
            List<Request.Message> ConversationHistory = new List<Request.Message>();

            while (true)
            {
                Console.Write("Сообщение: ");
                string UserInput = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(UserInput))
                    continue;

                if (UserInput.StartsWith("/img", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleImageRequest(UserInput);
                }
                else
                {
                    await HandleTextRequest(UserInput, ConversationHistory);
                }
            }
        }

        static async Task HandleImageRequest(string userInput)
        {
            string prompt = userInput.Substring(4).Trim();

            if (string.IsNullOrWhiteSpace(prompt))
            {
                Console.WriteLine("Введите описание после /img");
                return;
            }

            var imgMessages = new List<Request.Message>()
            {
                new Request.Message() { role = "user", content = prompt }
            };

            try
            {
                string outPath = await GetPictureAndSave(Token, imgMessages, ClientId);
                if (!string.IsNullOrEmpty(outPath) && File.Exists(outPath))
                {
                    Console.WriteLine($"Изображение сохранено: {outPath}");
                    Console.WriteLine("Устанавливаю изображение на рабочий стол...");

                    try
                    {
                        WallpaperSetter.SetWallpaper(outPath);
                        Console.WriteLine("Обои успешно установлены!");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка при установке обоев: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("Не удалось сохранить изображение");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при генерации изображения: {ex.Message}");
            }
        }

        static async Task HandleTextRequest(string userInput, List<Request.Message> conversationHistory)
        {
            conversationHistory.Add(new Request.Message()
            {
                role = "user",
                content = userInput
            });

            try
            {
                var response = await GetAnswer(Token, conversationHistory);
                if (response != null && response.choices != null && response.choices.Count > 0)
                {
                    string assistantResponse = response.choices[0].message.content;
                    Console.WriteLine($"Ответ: {assistantResponse}");

                    conversationHistory.Add(new Request.Message()
                    {
                        role = "assistant",
                        content = assistantResponse
                    });
                }
                else
                {
                    Console.WriteLine("Не удалось получить ответ от модели");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при получении ответа: {ex.Message}");
            }
        }

        public static async Task<string> GetToken(string rqUID, string bearer)
        {
            string ReturnToken = null;
            string Url = "https://ngw.devices.sberbank.ru:9443/api/v2/oauth";
            using (HttpClientHandler Handler = new HttpClientHandler())
            {
                Handler.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyError) => true;
                using (HttpClient client = new HttpClient(Handler))
                {
                    HttpRequestMessage Request = new HttpRequestMessage(HttpMethod.Post, Url);
                    Request.Headers.Add("Accept", "application/json");
                    Request.Headers.Add("RqUID", rqUID);
                    Request.Headers.Add("Authorization", $"Bearer {bearer}");
                    var Data = new List<KeyValuePair<string, string>>
                    {
                       new KeyValuePair<string, string>("scope", "GIGACHAT_API_PERS")
                    };
                    Request.Content = new FormUrlEncodedContent(Data);
                    HttpResponseMessage Response = await client.SendAsync(Request);
                    if (Response.IsSuccessStatusCode)
                    {
                        string ResponseContent = await Response.Content.ReadAsStringAsync();
                        ResponseToken Token = JsonConvert.DeserializeObject<ResponseToken>(ResponseContent);
                        ReturnToken = Token.access_token;
                    }
                    else
                    {
                        Console.WriteLine($"Ошибка при получении токена: {Response.StatusCode}");
                        Console.WriteLine(await Response.Content.ReadAsStringAsync());
                    }
                }
            }
            return ReturnToken;
        }

        public static async Task<ResponseMessage> GetAnswer(string token, List<Request.Message> messages)
        {
            ResponseMessage responseMessage = null;
            string Url = "https://gigachat.devices.sberbank.ru/api/v1/chat/completions";
            using (HttpClientHandler Handler = new HttpClientHandler())
            {
                Handler.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true;
                using (HttpClient client = new HttpClient(Handler))
                {
                    HttpRequestMessage Request = new HttpRequestMessage(HttpMethod.Post, Url);
                    Request.Headers.Add("Accept", "application/json");
                    Request.Headers.Add("Authorization", $"Bearer {token}");
                    Request.Headers.Add("X-Client-ID", ClientId);

                    Models.Request DataRequest = new Models.Request()
                    {
                        model = "GigaChat",
                        stream = false,
                        repetition_penalty = 1,
                        messages = messages
                    };

                    string JsonContent = JsonConvert.SerializeObject(DataRequest);
                    Request.Content = new StringContent(JsonContent, Encoding.UTF8, "application/json");

                    HttpResponseMessage Response = await client.SendAsync(Request);

                    if (Response.IsSuccessStatusCode)
                    {
                        string ResponseContent = await Response.Content.ReadAsStringAsync();
                        responseMessage = JsonConvert.DeserializeObject<ResponseMessage>(ResponseContent);
                    }
                    else
                    {
                        Console.WriteLine($"Ошибка API: {Response.StatusCode}");
                        Console.WriteLine(await Response.Content.ReadAsStringAsync());
                    }
                }
            }
            return responseMessage;
        }

        public static async Task<string> GetPictureAndSave(string token, List<Models.Request.Message> messages, string clientId = null)
        {
            if (string.IsNullOrEmpty(token)) throw new ArgumentNullException(nameof(token));
            string baseUrl = "https://gigachat.devices.sberbank.ru/api/v1";

            using (var handler = new HttpClientHandler())
            {
                handler.ServerCertificateCustomValidationCallback = (msg, cert, chain, ssl) => true;

                using (var http = new HttpClient(handler))
                {
                    http.DefaultRequestHeaders.Clear();
                    http.DefaultRequestHeaders.Add("Accept", "application/json");
                    http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                    if (!string.IsNullOrEmpty(clientId))
                    {
                        http.DefaultRequestHeaders.Add("X-Client-ID", clientId);
                    }

                    var payload = new
                    {
                        model = "GigaChat",
                        messages = messages,
                        function_call = "auto"
                    };

                    var jsonPayload = JsonConvert.SerializeObject(payload);
                    using (var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json"))
                    {
                        var postUrl = $"{baseUrl}/chat/completions";
                        var resp = await http.PostAsync(postUrl, content);

                        if (!resp.IsSuccessStatusCode)
                        {
                            Console.WriteLine($"Ошибка при создании изображения: {resp.StatusCode}");
                            var errBody = await resp.Content.ReadAsStringAsync();
                            Console.WriteLine(errBody);
                            return null;
                        }

                        var respJson = await resp.Content.ReadAsStringAsync();
                        string htmlContent = null;
                        try
                        {
                            var j = JObject.Parse(respJson);
                            htmlContent = j["choices"]?[0]?["message"]?["content"]?.ToString();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Не удалось распарсить ответ completions: " + ex.Message);
                            return null;
                        }

                        if (string.IsNullOrEmpty(htmlContent))
                        {
                            Console.WriteLine("В ответе нет содержимого с тегом <img>.");
                            return null;
                        }


                        var m = Regex.Match(htmlContent, "<img\\s+[^>]*src\\s*=\\s*['\"]([^'\"]+)['\"]", RegexOptions.IgnoreCase);
                        if (!m.Success)
                        {
                            Console.WriteLine("Тег <img> не найден в ответе или у него нет src.");
                            return null;
                        }

                        string fileId = m.Groups[1].Value;

                        if (string.IsNullOrEmpty(fileId))
                        {
                            Console.WriteLine("Не удалось извлечь file_id из тега <img>.");
                            return null;
                        }

                        var fileUrl = $"{baseUrl}/files/{fileId}/content";
                        using (var request = new HttpRequestMessage(HttpMethod.Get, fileUrl))
                        {
                            request.Headers.Add("Accept", "application/jpg");
                            if (!string.IsNullOrEmpty(clientId))
                            {
                                request.Headers.Add("X-Client-ID", clientId);
                            }

                            var fileResp = await http.SendAsync(request);
                            if (!fileResp.IsSuccessStatusCode)
                            {
                                Console.WriteLine($"Ошибка при скачивании изображения: {fileResp.StatusCode}");
                                var body = await fileResp.Content.ReadAsStringAsync();
                                Console.WriteLine(body);
                                return null;
                            }

                            var bytes = await fileResp.Content.ReadAsByteArrayAsync();
                            string outPath = Path.Combine(Environment.CurrentDirectory, $"gigachat_{Guid.NewGuid()}.jpg");
                            File.WriteAllBytes(outPath, bytes);
                            return outPath;
                        }
                    }
                }
            }
        }
    }
}