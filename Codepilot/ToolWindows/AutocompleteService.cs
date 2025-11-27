using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;

namespace Codepilot.ToolWindows
{
    /// <summary>
    /// Сервис автодополнения, который отслеживает изменения в файлах
    /// </summary>
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    public class AutocompleteService : IWpfTextViewCreationListener
    {
        private static AutocompleteService _instance;

        /// <summary>
        /// Получение экземпляра сервиса автодополнения
        /// </summary>
        public static AutocompleteService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new AutocompleteService();
                }
                return _instance;
            }
        }

        /// <summary>
        /// Конструктор
        /// </summary>
        public AutocompleteService()
        {
            Debug.WriteLine("AutocompleteService: Инициализация сервиса автодополнения");
            
            // Регистрируем сервис в пакете расширения
            ThreadHelper.ThrowIfNotOnUIThread();
            RegisterWithPackage();
        }

        /// <summary>
        /// Регистрация сервиса в пакете расширения
        /// </summary>
        private void RegisterWithPackage()
        {
            try
            {
                Debug.WriteLine("AutocompleteService: Регистрация в пакете расширения");
                
                // Здесь можно добавить код для регистрации сервиса в пакете расширения
                // Например, подписаться на события открытия/закрытия решения и т.д.
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AutocompleteService: Ошибка при регистрации в пакете расширения: {ex.Message}");
            }
        }

        /// <summary>
        /// Обработчик создания текстового представления
        /// </summary>
        /// <param name="textView">Текстовое представление</param>
        public void TextViewCreated(IWpfTextView textView)
        {
            Debug.WriteLine("AutocompleteService: Создано новое текстовое представление");
            
            // Подписываемся на события изменения текста
            textView.TextBuffer.Changed += TextBuffer_Changed;
            
            // Подписываемся на событие закрытия представления для отписки от событий
            textView.Closed += (sender, args) =>
            {
                textView.TextBuffer.Changed -= TextBuffer_Changed;
            };
        }

        /// <summary>
        /// Обработчик изменения текста в буфере
        /// </summary>
        /// <param name="sender">Отправитель</param>
        /// <param name="e">Аргументы события</param>
        private void TextBuffer_Changed(object sender, TextContentChangedEventArgs e)
        {
            try
            {
                // Получаем буфер текста
                ITextBuffer textBuffer = sender as ITextBuffer;
                if (textBuffer == null)
                    return;

                // Получаем весь текст из буфера
                string text = textBuffer.CurrentSnapshot.GetText();
                
                // Обрабатываем автодополнение и выводим информацию в консоль
                ProcessAutocomplete(text);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AutocompleteService: Ошибка при обработке изменения текста: {ex.Message}");
            }
        }

        /// <summary>
        /// Обработка автодополнения и вывод информации в консоль
        /// </summary>
        /// <param name="text">Текст файла</param>
        private void ProcessAutocomplete(string text)
        {
            try
            {
                // Получаем текущий файл
                ThreadHelper.ThrowIfNotOnUIThread();
                var dte = Package.GetGlobalService(typeof(DTE)) as DTE;
                if (dte == null || dte.ActiveDocument == null)
                    return;

                string filePath = dte.ActiveDocument.FullName;
                string fileName = Path.GetFileName(filePath);
                
                // Выводим в консоль сообщение "autocomplete/complete"
                Debug.WriteLine("autocomplete/complete");
                
                // Выводим содержимое файла
                Debug.WriteLine(text);
                
                // Выводим дополнительную информацию о файле
                Debug.WriteLine($"Файл: {filePath}");
                Debug.WriteLine($"Имя файла: {fileName}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AutocompleteService: Ошибка при обработке автодополнения: {ex.Message}");
            }
        }
    }
}
