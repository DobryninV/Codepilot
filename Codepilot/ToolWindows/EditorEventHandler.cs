using Codepilot.ToolWindows;
using EnvDTE;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Diagnostics;
using System.Windows.Input;

namespace Codepilot
{
    /// <summary>
    /// Обработчик событий редактора
    /// </summary>
    public class EditorEventHandler
    {
        private readonly AutocompleteService _autocompleteService;
        private IWpfTextView _activeTextView;
        private bool _isInitialized = false;

        /// <summary>
        /// Конструктор
        /// </summary>
        /// <param name="autocompleteService">Сервис автодополнения</param>
        public EditorEventHandler(AutocompleteService autocompleteService)
        {
            _autocompleteService = autocompleteService;
            InitializeEventHandlers();
        }

        /// <summary>
        /// Инициализация обработчиков событий
        /// </summary>
        private void InitializeEventHandlers()
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                // Получаем DTE (Development Tools Environment)
                var dte = Package.GetGlobalService(typeof(DTE)) as DTE;
                if (dte == null)
                {
                    Debug.WriteLine("Failed to get DTE service");
                    return;
                }

                // Подписываемся на событие активации окна
                dte.Events.WindowEvents.WindowActivated += WindowEvents_WindowActivated;

                // Подписываемся на событие изменения текста
                dte.Events.TextEditorEvents.LineChanged += TextEditorEvents_LineChanged;

                _isInitialized = true;
                Debug.WriteLine("Editor event handlers initialized");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing editor event handlers: {ex.Message}");
            }
        }

        /// <summary>
        /// Обработчик события активации окна
        /// </summary>
        /// <param name="gotFocus">Окно, получившее фокус</param>
        /// <param name="lostFocus">Окно, потерявшее фокус</param>
        private void WindowEvents_WindowActivated(EnvDTE.Window gotFocus, EnvDTE.Window lostFocus)
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                // Проверяем, что активировано окно документа
                if (gotFocus.Kind == "Document")
                {
                    // Получаем активное текстовое представление
                    _activeTextView = GetActiveTextView();
                    if (_activeTextView != null)
                    {
                        // Подписываемся на события клавиатуры
                        _activeTextView.VisualElement.KeyDown += VisualElement_KeyDown;
                        Debug.WriteLine("Subscribed to key events for active text view");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in WindowEvents_WindowActivated: {ex.Message}");
            }
        }

        /// <summary>
        /// Обработчик события изменения строки
        /// </summary>
        /// <param name="startPoint">Начальная точка</param>
        /// <param name="endPoint">Конечная точка</param>
        /// <param name="hint">Подсказка</param>
        private void TextEditorEvents_LineChanged(TextPoint startPoint, TextPoint endPoint, int hint)
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                // Проверяем, что пользователь ввел символ, который может вызвать автодополнение
                // Например, точку, открывающую скобку и т.д.
                var document = startPoint.Parent as TextDocument;
                if (document != null)
                {
                    var line = document.CreateEditPoint(startPoint).GetLines(startPoint.Line, startPoint.Line + 1);
                    if (!string.IsNullOrEmpty(line))
                    {
                        var lastChar = line.Trim().Length > 0 ? line.Trim()[line.Trim().Length - 1] : '\0';
                        if (lastChar == '.' || lastChar == '(' || lastChar == '{' || lastChar == '[')
                        {
                            // Вызываем автодополнение
                            _autocompleteService.TriggerCompletion();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in TextEditorEvents_LineChanged: {ex.Message}");
            }
        }

        /// <summary>
        /// Обработчик события нажатия клавиши
        /// </summary>
        /// <param name="sender">Отправитель</param>
        /// <param name="e">Аргументы события</param>
        private void VisualElement_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            try
            {
                // Проверяем, что нажата клавиша Tab
                if (e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    // Вызываем автодополнение
                    _autocompleteService.TriggerCompletion();
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in VisualElement_KeyDown: {ex.Message}");
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
                ThreadHelper.ThrowIfNotOnUIThread();

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
    }
}
