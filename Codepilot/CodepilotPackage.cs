global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using System;
global using Task = System.Threading.Tasks.Task;
using System.Runtime.InteropServices;
using System.Threading;

namespace Codepilot
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
    [ProvideToolWindow(typeof(MyToolWindow.Pane), Style = VsDockStyle.Tabbed, Window = WindowGuids.SolutionExplorer)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuids.CodepilotString)]
    public sealed class CodepilotPackage : ToolkitPackage
    {
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.RegisterCommandsAsync();

            this.RegisterToolWindows();
            
            // Инициализируем сервис автодополнения
            await InitializeAutocompleteServiceAsync();
        }
        
        /// <summary>
        /// Инициализация сервиса автодополнения
        /// </summary>
        private async Task InitializeAutocompleteServiceAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            try
            {
                // Создаем экземпляр сервиса автодополнения
                var autocompleteService = Codepilot.ToolWindows.AutocompleteService.Instance;
                System.Diagnostics.Debug.WriteLine("CodepilotPackage: Сервис автодополнения инициализирован");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CodepilotPackage: Ошибка при инициализации сервиса автодополнения: {ex.Message}");
            }
        }
    }
}
