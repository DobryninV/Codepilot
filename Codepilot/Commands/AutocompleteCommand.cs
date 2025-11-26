using Codepilot.ToolWindows;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Windows.Input;
using Task = System.Threading.Tasks.Task;

namespace Codepilot.Commands
{
    /// <summary>
    /// Команда для вызова автодополнения
    /// </summary>
    internal sealed class AutocompleteCommand
    {
        /// <summary>
        /// Идентификатор команды
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Группа команд
        /// </summary>
        public static readonly Guid CommandSet = new Guid("c8f5bc35-5d92-4c5d-a292-85a2d658a8f7");

        /// <summary>
        /// Экземпляр команды
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Запрет создания экземпляра
        /// </summary>
        private AutocompleteCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Инициализация команды
        /// </summary>
        /// <param name="package">Пакет расширения</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Переключаемся в UI поток
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new AutocompleteCommand(package, commandService);
        }

        /// <summary>
        /// Получение экземпляра команды
        /// </summary>
        public static AutocompleteCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Получение сервиса
        /// </summary>
        /// <typeparam name="T">Тип сервиса</typeparam>
        /// <returns>Сервис</returns>
        private T GetService<T>() where T : class
        {
            return package.GetServiceAsync(typeof(T)).Result as T;
        }

        /// <summary>
        /// Выполнение команды
        /// </summary>
        /// <param name="sender">Отправитель</param>
        /// <param name="e">Аргументы события</param>
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                // Получаем окно инструментов
                var toolWindowPane = package.FindToolWindow(typeof(MyToolWindow), 0, true);
                if (toolWindowPane?.Content is MyToolWindowControl control)
                {
                    // Получаем сервис автодополнения
                    var autocompleteService = control.GetAutocompleteService();
                    if (autocompleteService != null)
                    {
                        // Вызываем автодополнение
                        autocompleteService.TriggerCompletion();
                    }
                    else
                    {
                        Debug.WriteLine("AutocompleteService is null");
                    }
                }
                else
                {
                    Debug.WriteLine("MyToolWindow not found");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error executing autocomplete command: {ex.Message}");
            }
        }
    }
}
