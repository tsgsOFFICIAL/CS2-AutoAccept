using System.Windows.Input;
using System.Windows;
using System;

namespace CS2_AutoAccept
{
    /// <summary>
    /// Interaction logic for HotkeyDialog.xaml
    /// </summary>
    public partial class HotkeyDialog : Window
    {
        public KeyGesture? SelectedHotkey { get; private set; }

        public HotkeyDialog()
        {
            InitializeComponent();
            HotkeyText.Text = "Press a key combination";
            KeyDown += HotkeyDialog_KeyDown;
        }

        private void HotkeyDialog_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.Escape || e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl || e.Key == Key.LeftAlt || e.Key == Key.RightAlt || e.Key == Key.LeftShift || e.Key == Key.RightShift)
                {
                    return;
                }

                ModifierKeys modifiers = Keyboard.Modifiers;
                SelectedHotkey = new KeyGesture(e.Key, modifiers);

                HotkeyText.Text = $"Selected Hotkey: {modifiers} + {e.Key}";

                DialogResult = true;
                Close();
            }
            catch (Exception)
            {
                DialogResult = false;
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

    }
}
