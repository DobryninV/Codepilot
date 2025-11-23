using Newtonsoft.Json;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace MyGui
{
    public partial class MyGuiControl
    {
        /// <summary>
        /// Класс для обмена сообщениями с Core
        /// </summary>
        public class CoreMessenger
        {
            private TcpClient _tcpClient;
            private StreamWriter _tcpWriter;
            private StreamReader _tcpReader;
            private System.Threading.Thread _readThread;
            private bool _isConnected;
            private Dictionary<string, TaskCompletionSource<object>> _pendingRequests = new Dictionary<string, TaskCompletionSource<object>>();
            private Dictionary<string, Action<Message>> _messageTypeHandlers = new Dictionary<string, Action<Message>>();
            private object _configCache;

            /// <summary>
            /// Список типов сообщений, которые могут приходить от Core к IDE
            /// </summary>
            private readonly List<string> _ideMessageTypes = new List<string>
        {
            "readRangeInFile",
            "isTelemetryEnabled",
            "getUniqueId",
            "getWorkspaceConfigs",
            "getDiff",
            "getTerminalContents",
            "getWorkspaceDirs",
            "showLines",
            "writeFile",
            "fileExists",
            "showVirtualFile",
            "openFile",
            "runCommand",
            "saveFile",
            "readFile",
            "showDiff",
            "getOpenFiles",
            "getCurrentFile",
            "getPinnedFiles",
            "getSearchResults",
            "getProblems",
            "subprocess",
            "getBranch",
            "getTags",
            "getIdeInfo",
            "getIdeSettings",
            "getRepoName",
            "listDir",
            "getGitRootPath",
            "getFileStats",
            "insertAtCursor",
            "applyToFile",
            "getGitHubAuthToken",
            "setGitHubAuthToken",
            "getControlPlaneSessionInfo",
            "logoutOfControlPlane",
            "getTerminalContents",
            "showToast",
            "openUrl"
        };

            /// <summary>
            /// Событие, возникающее при получении сообщения от Core
            /// </summary>
            public event EventHandler<Message> MessageReceived;

            /// <summary>
            /// Конструктор
            /// </summary>
            public CoreMessenger()
            {
                // Регистрируем обработчики сообщений
                RegisterMessageHandlers();

                // Подключаемся к Core
                ConnectToCore();
            }

            /// <summary>
            /// Регистрация обработчиков сообщений
            /// </summary>
            private void RegisterMessageHandlers()
            {
                // Обработчик для ping-сообщений
                _messageTypeHandlers["ping"] = (message) =>
                {
                    SendToCore("pong", null, message.MessageId);
                };

                // Обработчик для сообщений о конфигурации
                _messageTypeHandlers["configUpdate"] = (message) =>
                {
                    _configCache = message.Data;
                    Debug.WriteLine("Received config update");
                };

                // Обработчик для сообщений о профилях
                _messageTypeHandlers["didChangeAvailableProfiles"] = (message) =>
                {
                    Debug.WriteLine("Profiles changed");
                };

                // Регистрация обработчиков для сообщений от Core к IDE
                RegisterIdeMessageHandlers();
            }

            /// <summary>
            /// Регистрация обработчиков для сообщений от Core к IDE
            /// </summary>
            private void RegisterIdeMessageHandlers()
            {
                // Базовая информация об IDE
                _messageTypeHandlers["getIdeInfo"] = (message) =>
                {
                    var response = new
                    {
                        name = "vs2022",
                        version = "1.0.0",
                        platform = Environment.OSVersion.Platform.ToString(),
                        arch = Environment.Is64BitOperatingSystem ? "x64" : "x86"
                    };
                    SendResponseToCore(message.MessageType, response, message.MessageId);
                };

                // Настройки IDE
                _messageTypeHandlers["getIdeSettings"] = (message) =>
                {
                    var response = new
                    {
                        theme = "dark", // Можно получить из настроек VS
                        fontSize = 14,
                        fontFamily = "Consolas, 'Courier New', monospace",
                        lineHeight = 1.5,
                        tabSize = 4,
                        insertSpaces = true,
                        wordWrap = "off",
                        autoSave = "off",
                        formatOnSave = false,
                        formatOnType = false,
                        formatOnPaste = false,
                        minimap = new
                        {
                            enabled = true,
                            side = "right",
                            showSlider = "always",
                            renderCharacters = true,
                            maxColumn = 120
                        }
                    };
                    SendResponseToCore(message.MessageType, response, message.MessageId);
                };

                // Получение директорий рабочего пространства
                _messageTypeHandlers["getWorkspaceDirs"] = (message) =>
                {
                    // Здесь нужно получить список открытых директорий в решении
                    // Для примера возвращаем текущую директорию
                    var response = new string[] { Directory.GetCurrentDirectory() };
                    SendPureResponseToCore(message.MessageType, response, message.MessageId);
                };

                // Получение открытых файлов
                _messageTypeHandlers["getOpenFiles"] = (message) =>
                {
                    // Здесь нужно получить список открытых файлов в VS
                    // Для примера возвращаем пустой список
                    var response = new string[] { };
                    SendResponseToCore(message.MessageType, response, message.MessageId);
                };

                // Получение текущего файла
                _messageTypeHandlers["getCurrentFile"] = (message) =>
                {
                    // Здесь нужно получить информацию о текущем открытом файле
                    // Для примера возвращаем пустой объект
                    var response = new
                    {
                        isUntitled = false,
                        path = "",
                        contents = ""
                    };
                    SendResponseToCore(message.MessageType, response, message.MessageId);
                };

                // Получение закрепленных файлов
                _messageTypeHandlers["getPinnedFiles"] = (message) =>
                {
                    var response = new string[] { };
                    SendResponseToCore(message.MessageType, response, message.MessageId);
                };

                // Проверка включенной телеметрии
                _messageTypeHandlers["isTelemetryEnabled"] = (message) =>
                {
                    var response = false;
                    SendResponseToCore(message.MessageType, response, message.MessageId);
                };

                // Получение уникального ID
                _messageTypeHandlers["getUniqueId"] = (message) =>
                {
                    var response = "vs2022-" + Guid.NewGuid().ToString();
                    SendResponseToCore(message.MessageType, response, message.MessageId);
                };

                // Получение конфигураций рабочего пространства
                _messageTypeHandlers["getWorkspaceConfigs"] = (message) =>
                {
                    var response = new object[] { };
                    SendResponseToCore(message.MessageType, response, message.MessageId);
                };

                // Получение содержимого терминала
                _messageTypeHandlers["getTerminalContents"] = (message) =>
                {
                    var response = "";
                    SendResponseToCore(message.MessageType, response, message.MessageId);
                };

                // Чтение файла
                _messageTypeHandlers["readFile"] = (message) =>
                {
                    try
                    {
                        var filepath = message.Data.ToString();
                        var content = File.ReadAllText(filepath);
                        SendResponseToCore(message.MessageType, content, message.MessageId);
                    }
                    catch (Exception ex)
                    {
                        SendErrorToCore(message.MessageType, ex.Message, message.MessageId);
                    }
                };

                // Проверка существования файла
                _messageTypeHandlers["fileExists"] = (message) =>
                {
                    try
                    {
                        var filepath = message.Data.ToString();
                        var exists = File.Exists(filepath);
                        SendResponseToCore(message.MessageType, exists, message.MessageId);
                    }
                    catch (Exception ex)
                    {
                        SendErrorToCore(message.MessageType, ex.Message, message.MessageId);
                    }
                };

                // Получение проблем
                _messageTypeHandlers["getProblems"] = (message) =>
                {
                    var response = new object[] { };
                    SendResponseToCore(message.MessageType, response, message.MessageId);
                };

                // Получение тегов
                _messageTypeHandlers["getTags"] = (message) =>
                {
                    var response = new string[] { };
                    SendResponseToCore(message.MessageType, response, message.MessageId);
                };

                // Получение текущей ветки
                _messageTypeHandlers["getBranch"] = (message) =>
                {
                    var response = "main"; // Здесь нужно получить текущую ветку из Git
                    SendResponseToCore(message.MessageType, response, message.MessageId);
                };

                // Получение имени репозитория
                _messageTypeHandlers["getRepoName"] = (message) =>
                {
                    var response = Path.GetFileName(Directory.GetCurrentDirectory());
                    SendResponseToCore(message.MessageType, response, message.MessageId);
                };

                // Получение корневого пути Git-репозитория
                _messageTypeHandlers["getGitRootPath"] = (message) =>
                {
                    var response = Directory.GetCurrentDirectory(); // Здесь нужно получить корневой путь Git-репозитория
                    SendResponseToCore(message.MessageType, response, message.MessageId);
                };

                // Получение списка файлов в директории
                _messageTypeHandlers["listDir"] = (message) =>
                {
                    try
                    {
                        string dir;
                        if (message.Data is Newtonsoft.Json.Linq.JObject jObject && jObject["dir"] != null)
                        {
                            dir = jObject["dir"].ToString();
                        } else
                        {
                            dir = message.Data.ToString();
                        } 
                        
                        Debug.WriteLine($"dir: {dir}");
                        var files = Directory.GetFiles(dir).Select(f => new[] { Path.GetFileName(f), "file" });
                        Debug.WriteLine($"files: {files}");
                        var dirs = Directory.GetDirectories(dir).Select(d => new[] { Path.GetFileName(d), "directory" });
                        Debug.WriteLine($"dirs: {dirs}");
                        var response = files.Concat(dirs).ToArray();
                        SendPureResponseToCore(message.MessageType, response, message.MessageId);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"dirs: {ex}");
                        SendErrorToCore(message.MessageType, ex.Message, message.MessageId);
                    }
                };

                // Получение статистики файла
                _messageTypeHandlers["getFileStats"] = (message) =>
                {
                    var response = new { };
                    SendResponseToCore(message.MessageType, response, message.MessageId);
                };

                // Получение токена авторизации GitHub
                _messageTypeHandlers["getGitHubAuthToken"] = (message) =>
                {
                    SendResponseToCore(message.MessageType, null, message.MessageId);
                };

                // Получение информации о сессии Control Plane
                _messageTypeHandlers["getControlPlaneSessionInfo"] = (message) =>
                {
                    SendResponseToCore(message.MessageType, null, message.MessageId);
                };

                // Открытие URL
                _messageTypeHandlers["openUrl"] = (message) =>
                {
                    try
                    {
                        var url = message.Data.ToString();
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                        SendResponseToCore(message.MessageType, true, message.MessageId);
                    }
                    catch (Exception ex)
                    {
                        SendErrorToCore(message.MessageType, ex.Message, message.MessageId);
                    }
                };

                // Показать уведомление
                _messageTypeHandlers["showToast"] = (message) =>
                {
                    // Здесь нужно показать уведомление в VS
                    SendResponseToCore(message.MessageType, true, message.MessageId);
                };
            }

            /// <summary>
            /// Подключение к Core через TCP
            /// </summary>
            private void ConnectToCore()
            {
                try
                {
                    // Адрес и порт для подключения к Core
                    string host = "127.0.0.1";
                    int port = 3000;

                    // Создаем TCP-клиент
                    _tcpClient = new TcpClient();

                    // Пытаемся подключиться с таймаутом
                    var connectTask = _tcpClient.ConnectAsync(host, port);
                    if (!Task.WaitAll(new Task[] { connectTask }, 5000))
                    {
                        throw new TimeoutException($"Connection to {host}:{port} timed out");
                    }

                    // Создаем потоки для чтения и записи
                    NetworkStream stream = _tcpClient.GetStream();
                    _tcpWriter = new StreamWriter(stream) { AutoFlush = true };
                    _tcpReader = new StreamReader(stream);

                    // Запускаем поток для чтения ответов
                    _readThread = new System.Threading.Thread(ReadTcpMessages);
                    _readThread.IsBackground = true;
                    _readThread.Start();

                    _isConnected = true;

                    Debug.WriteLine($"Connected to Core via TCP at {host}:{port}");

                    // Запрашиваем конфигурацию после подключения
                    RequestConfig();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error connecting to Core: {ex.Message}");
                    _isConnected = false;
                }
            }

            /// <summary>
            /// Запрос конфигурации от Core
            /// </summary>
            private async void RequestConfig()
            {
                try
                {
                    // Запрашиваем список профилей
                    var profiles = await RequestFromCore("config/listProfiles", null);
                    Debug.WriteLine($"Received profiles: {JsonConvert.SerializeObject(profiles)}");

                    // Запрашиваем текущую конфигурацию
                    _configCache = await RequestFromCore("config/getSerializedProfileInfo", new { profileId = "default" });
                    Debug.WriteLine($"Received config: {JsonConvert.SerializeObject(_configCache)}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error requesting config: {ex.Message}");
                }
            }

            /// <summary>
            /// Получение кэшированной конфигурации
            /// </summary>
            /// <returns>Конфигурация</returns>
            public object GetConfig()
            {
                return _configCache;
            }

            /// <summary>
            /// Чтение сообщений из TCP-соединения
            /// </summary>
            private void ReadTcpMessages()
            {
                try
                {
                    while (_isConnected && _tcpClient.Connected)
                    {
                        string line = _tcpReader.ReadLine();
                        if (string.IsNullOrEmpty(line))
                            continue;

                        // Обрабатываем полученное сообщение
                        ProcessReceivedMessage(line);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error reading from TCP: {ex.Message}");
                    _isConnected = false;

                    // Пробуем переподключиться
                    Task.Run(() =>
                    {
                        System.Threading.Thread.Sleep(5000); // Ждем 5 секунд перед повторным подключением
                        ConnectToCore();
                    });
                }
            }

            /// <summary>
            /// Обработка полученного сообщения
            /// </summary>
            /// <param name="json">JSON-строка с сообщением</param>
            private void ProcessReceivedMessage(string json)
            {
                try
                {
                    // Парсим JSON
                    var message = JsonConvert.DeserializeObject<Message>(json);
                    if (message == null)
                        return;

                    Debug.WriteLine($"Received message: {message.MessageType}, ID: {message.MessageId}");

                    // Если есть ожидающий запрос, завершаем его
                    if (_pendingRequests.TryGetValue(message.MessageId, out var tcs))
                    {
                        tcs.SetResult(message.Data);
                        _pendingRequests.Remove(message.MessageId);
                    }
                    else
                    {
                        // Если есть обработчик для данного типа сообщения, вызываем его
                        if (_messageTypeHandlers.TryGetValue(message.MessageType, out var handler))
                        {
                            handler(message);
                        }

                        // Вызываем событие получения сообщения
                        MessageReceived?.Invoke(this, message);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error processing message: {ex.Message}");
                }
            }

            /// <summary>
            /// Отправка сообщения в Core
            /// </summary>
            /// <param name="messageType">Тип сообщения</param>
            /// <param name="data">Данные сообщения</param>
            /// <param name="messageId">Идентификатор сообщения</param>
            public void SendToCore(string messageType, object data, string messageId = null)
            {
                if (!_isConnected || !_tcpClient.Connected)
                {
                    Debug.WriteLine("Not connected to Core");

                    // Пробуем переподключиться
                    ConnectToCore();

                    if (!_isConnected || !_tcpClient.Connected)
                    {
                        Debug.WriteLine("Failed to reconnect to Core");
                        return;
                    }
                }

                try
                {
                    // Создаем сообщение
                    var message = new Message
                    {
                        MessageId = messageId ?? Guid.NewGuid().ToString(),
                        MessageType = messageType,
                        Data = data
                    };

                    // Сериализуем сообщение в JSON
                    var json = JsonConvert.SerializeObject(message);

                    // Отправляем сообщение в Core
                    _tcpWriter.WriteLine(json);

                    Debug.WriteLine($"Sent message to Core: {messageType}, ID: {message.MessageId}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error sending message to Core: {ex.Message}");

                    // Помечаем соединение как разорванное
                    _isConnected = false;

                    // Пробуем переподключиться
                    Task.Run(() =>
                    {
                        System.Threading.Thread.Sleep(5000); // Ждем 5 секунд перед повторным подключением
                        ConnectToCore();
                    });
                }
            }

            /// <summary>
            /// Отправка успешного ответа в Core
            /// </summary>
            /// <param name="messageType">Тип сообщения</param>
            /// <param name="content">Содержимое ответа</param>
            /// <param name="messageId">Идентификатор сообщения</param>
            private void SendResponseToCore(string messageType, object content, string messageId)
            {
                var response = new
                {
                    done = true,
                    content = content,
                    status = "success"
                };
                SendToCore(messageType, response, messageId);
            }

            private void SendPureResponseToCore(string messageType, object content, string messageId)
            {
                SendToCore(messageType, content, messageId);
            }

            /// <summary>
            /// Отправка ошибки в Core
            /// </summary>
            /// <param name="messageType">Тип сообщения</param>
            /// <param name="error">Текст ошибки</param>
            /// <param name="messageId">Идентификатор сообщения</param>
            private void SendErrorToCore(string messageType, string error, string messageId)
            {
                var response = new
                {
                    done = true,
                    error = error,
                    status = "error"
                };
                SendToCore(messageType, response, messageId);
            }

            /// <summary>
            /// Отправка запроса в Core и ожидание ответа
            /// </summary>
            /// <param name="messageType">Тип сообщения</param>
            /// <param name="data">Данные сообщения</param>
            /// <returns>Ответ от Core</returns>
            public async Task<object> RequestFromCore(string messageType, object data)
            {
                if (!_isConnected)
                {
                    // Пробуем переподключиться
                    ConnectToCore();
                    if (!_isConnected)
                    {
                        throw new InvalidOperationException("Not connected to Core");
                    }
                }

                var messageId = Guid.NewGuid().ToString();
                var tcs = new TaskCompletionSource<object>();
                _pendingRequests[messageId] = tcs;

                SendToCore(messageType, data, messageId);

                // Ожидаем ответ с таймаутом
                var timeoutTask = Task.Delay(10000);
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    _pendingRequests.Remove(messageId);
                    throw new TimeoutException($"Request to Core timed out: {messageType}");
                }

                return await tcs.Task;
            }
        }

        /// <summary>
        /// Класс сообщения
        /// </summary>
        public class Message
        {
            /// <summary>
            /// Идентификатор сообщения
            /// </summary>
            [JsonProperty("messageId")]
            public string MessageId { get; set; }

            /// <summary>
            /// Тип сообщения
            /// </summary>
            [JsonProperty("messageType")]
            public string MessageType { get; set; }

            /// <summary>
            /// Данные сообщения
            /// </summary>
            [JsonProperty("data")]
            public object Data { get; set; }
        }
    }
}
