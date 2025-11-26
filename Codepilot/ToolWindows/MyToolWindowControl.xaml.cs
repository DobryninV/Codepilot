using Codepilot.ToolWindows;
using MyGui;
using System.Windows;
using System.Windows.Controls;

namespace Codepilot
{
    public partial class MyToolWindowControl : UserControl
    {
        public MyToolWindowControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Получение GUI контрола
        /// </summary>
        public MyGuiControl GuiControl => guiControl;

        /// <summary>
        /// Получение сервиса автодополнения
        /// </summary>
        public AutocompleteService GetAutocompleteService()
        {
            // Получаем приватное поле _autocompleteService через рефлексию
            var autocompleteServiceField = guiControl.GetType().GetField("_autocompleteService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (autocompleteServiceField != null)
            {
                return autocompleteServiceField.GetValue(guiControl) as AutocompleteService;
            }
            return null;
        }
    }
}
