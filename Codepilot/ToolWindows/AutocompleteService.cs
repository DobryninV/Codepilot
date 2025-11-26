using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Threading;
using static MyGui.MyGuiControl;

namespace Codepilot.ToolWindows
{
    /// <summary>
    /// Сервис автодополнения для VS2022
    /// </summary>
    public class AutocompleteService
    {
        private CoreMessenger _coreMessenger;
        private string _pendingCompletionId;
        private string _pendingCompletionText;
        private bool _isCompletionActive = false;
        private DispatcherTimer _debounceTimer;
        private const int DEBOUNCE_DELAY_MS = 300;

        /// <summary>
        /// Конструктор
        /// </summary>
        /// <param name="coreMessenger">Мессенджер для связи с Core</param>
        public AutocompleteService(CoreMessenger coreMessenger)
        {
            _coreMessenger = coreMessenger;
            _debounceTimer = new DispatcherTimer();
            _debounceTimer.Interval = TimeSpan.FromMilliseconds(DEBOUNCE_DELAY_MS);
            _debounceTimer.Tick += (sender, args) =>
            {
                _debounceTimer.Stop();
                TriggerCompletionInternal();
            };
        }

        /// <summary>
        /// Запуск автодополнения с дебаунсом
        /// </summary>
        public void TriggerCompletion()
        {
            // Сбрасываем таймер дебаунса
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        /// <summary>
        /// Внутренний метод запуска автодополнения
        /// </summary>
        private async void TriggerCompletionInternal()
        {
            try
            {
                // Получаем активный документ и позицию курсора
                var (document, position) = GetActiveDocumentAndPosition();
                if (document == null)
                {
                    Debug.WriteLine("No active document");
                    return;
                }

                // Если уже есть активное автодополнение, отменяем его
                if (_isCompletionActive)
                {
                    CancelCompletion();
                }

                // Создаем уникальный ID для запроса автодополнения
                _pendingCompletionId = Guid.NewGuid().ToString();
                _isCompletionActive = true;

                // Получаем путь к файлу
                string filePath = document.FullName;
                Uri fileUri = new Uri(filePath);

                // Создаем входные данные для запроса автодополнения
                var input = new Dictionary<string, object>
                {
                    ["completionId"] = _pendingCompletionId,
                    ["filepath"] = fileUri.ToString(),
                    ["pos"] = new Dictionary<string, int>
                    {
                        ["line"] = position.Line,
                        ["character"] = position.Character
                    },
                    ["isUntitledFile"] = false,
                    ["recentlyVisitedRanges"] = new object[] { },
                    ["recentlyEditedRanges"] = new object[] { }
                };

                // Отправляем запрос на автодополнение в Core
                _coreMessenger.RequestWithCallback(
                    "autocomplete/complete",
                    input,
                    _pendingCompletionId,
                    HandleCompletionResponse
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error triggering completion: {ex.Message}");
                _isCompletionActive = false;
            }
        }

        /// <summary>
        /// Обработка ответа на запрос автодополнения
        /// </summary>
        /// <param name="response">Ответ от Core</param>
        private void HandleCompletionResponse(object response)
        {
            try
            {
                // Проверяем, что ответ не пустой
                if (response == null)
                {
                    Debug.WriteLine("Empty completion response");
                    _isCompletionActive = false;
                    return;
                }

                // Преобразуем ответ в словарь
                var responseDict = response as Newtonsoft.Json.Linq.JObject;
                if (responseDict == null)
                {
                    Debug.WriteLine("Invalid completion response format");
                    _isCompletionActive = false;
                    return;
                }

                // Получаем содержимое ответа
                var content = responseDict["content"];
                if (content == null || !content.HasValues)
                {
                    Debug.WriteLine("No completion content");
                    _isCompletionActive = false;
                    return;
                }

                // Получаем текст автодополнения
                var completions = content.ToObject<List<string>>();
                if (completions == null || completions.Count == 0)
                {
                    Debug.WriteLine("No completions in response");
                    _isCompletionActive = false;
                    return;
                }

                // Сохраняем текст автодополнения
                _pendingCompletionText = completions[0];

                // Отображаем автодополнение
                DisplayCompletion(_pendingCompletionText);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling completion response: {ex.Message}");
                _isCompletionActive = false;
            }
        }

        /// <summary>
        /// Отображение автодополнения
        /// </summary>
        /// <param name="completionText">Текст автодополнения</param>
        private void DisplayCompletion(string completionText)
        {
            try
            {
                // Получаем активный документ и позицию курсора
                var (document, position) = GetActiveDocumentAndPosition();
                if (document == null)
                {
                    Debug.WriteLine("No active document for displaying completion");
                    _isCompletionActive = false;
                    return;
                }

                // Получаем текстовый редактор
                var textView = GetActiveTextView();
                if (textView == null)
                {
                    Debug.WriteLine("No active text view");
                    _isCompletionActive = false;
                    return;
                }

                // Для простой реализации просто вставляем текст автодополнения
                // В будущем можно реализовать отображение в виде подсказки
                ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    // Вставляем текст автодополнения
                    textView.TextBuffer.Insert(textView.Caret.Position.BufferPosition.Position, completionText);

                    // Отправляем уведомление о принятии автодополнения
                    AcceptCompletion();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error displaying completion: {ex.Message}");
                _isCompletionActive = false;
            }
        }

        /// <summary>
        /// Принятие автодополнения
        /// </summary>
        public void AcceptCompletion()
        {
            if (!_isCompletionActive || string.IsNullOrEmpty(_pendingCompletionId))
            {
                return;
            }

            try
            {
                // Отправляем запрос на принятие автодополнения в Core
                _coreMessenger.RequestWithCallback(
                    "autocomplete/accept",
                    new Dictionary<string, string> { ["completionId"] = _pendingCompletionId },
                    Guid.NewGuid().ToString(),
                    (response) =>
                    {
                        Debug.WriteLine("Completion accepted");
                        _isCompletionActive = false;
                        _pendingCompletionId = null;
                        _pendingCompletionText = null;
                    }
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error accepting completion: {ex.Message}");
                _isCompletionActive = false;
            }
        }

        /// <summary>
        /// Отмена автодополнения
        /// </summary>
        public void CancelCompletion()
        {
            if (!_isCompletionActive || string.IsNullOrEmpty(_pendingCompletionId))
            {
                return;
            }

            try
            {
                // Отправляем запрос на отмену автодополнения в Core
                _coreMessenger.RequestWithCallback(
                    "autocomplete/cancel",
                    null,
                    Guid.NewGuid().ToString(),
                    (response) =>
                    {
                        Debug.WriteLine("Completion cancelled");
                        _isCompletionActive = false;
                        _pendingCompletionId = null;
                        _pendingCompletionText = null;
                    }
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cancelling completion: {ex.Message}");
                _isCompletionActive = false;
            }
        }

        /// <summary>
        /// Получение активного документа и позиции курсора
        /// </summary>
        /// <returns>Кортеж с документом и позицией</returns>
        private (EnvDTE.Document document, TextPosition position) GetActiveDocumentAndPosition()
        {
            try
            {
                // Получаем DTE (Development Tools Environment)
                var dte = Package.GetGlobalService(typeof(DTE)) as DTE;
                if (dte == null)
                {
                    Debug.WriteLine("Failed to get DTE service");
                    return (null, null);
                }

                // Получаем активный документ
                var document = dte.ActiveDocument;
                if (document == null)
                {
                    Debug.WriteLine("No active document");
                    return (null, null);
                }

                // Получаем текстовый документ
                var textDocument = document.Object("TextDocument") as EnvDTE.TextDocument;
                if (textDocument == null)
                {
                    Debug.WriteLine("Active document is not a text document");
                    return (null, null);
                }

                // Получаем позицию курсора
                var selection = textDocument.Selection;
                if (selection == null)
                {
                    Debug.WriteLine("No selection in text document");
                    return (null, null);
                }

                // Создаем объект с позицией
                var position = new TextPosition
                {
                    Line = selection.ActivePoint.Line - 1, // VS использует 1-based индексы, а Core - 0-based
                    Character = selection.ActivePoint.DisplayColumn - 1
                };

                return (document, position);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting active document and position: {ex.Message}");
                return (null, null);
            }
        }

        /// <summary>
        /// Получение активного текстового представления
        /// </summary>
        /// <returns>Активное текстовое представление</returns>
        private IWpfTextView GetActiveTextView()
        {
            try
            {
                // Получаем сервис текстового менеджера
                var textManager = Package.GetGlobalService(typeof(SVsTextManager)) as IVsTextManager;
                if (textManager == null)
                {
                    Debug.WriteLine("Failed to get text manager service");
                    return null;
                }

                // Получаем активное представление
                IVsTextView vsTextView = null;
                textManager.GetActiveView(1, null, out vsTextView);
                if (vsTextView == null)
                {
                    Debug.WriteLine("No active text view");
                    return null;
                }

                // Преобразуем в WPF представление
                IWpfTextView wpfTextView = null;
                var userData = vsTextView as IVsUserData;
                if (userData != null)
                {
                    var guidViewHost = Microsoft.VisualStudio.Editor.DefGuidList.guidIWpfTextViewHost;
                    object host;
                    userData.GetData(ref guidViewHost, out host);
                    var textViewHost = host as Microsoft.VisualStudio.Text.Editor.IWpfTextViewHost;
                    if (textViewHost != null)
                    {
                        wpfTextView = textViewHost.TextView;
                    }
                }

                return wpfTextView;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting active text view: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Класс для представления позиции в тексте
        /// </summary>
        private class TextPosition
        {
            public int Line { get; set; }
            public int Character { get; set; }
        }
    }
}
