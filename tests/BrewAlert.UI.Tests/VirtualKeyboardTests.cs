using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BrewAlert.UI.Views;
using CommunityToolkit.Mvvm.Input;
using Xunit;

namespace BrewAlert.UI.Tests;

public class VirtualKeyboardTests
{
    [AvaloniaFact]
    public void TypingTurkishCharacters_SpaceAndBackspace_UpdatesTextWithSmartShift()
    {
        var doneInvoked = false;
        var keyboard = new VirtualKeyboardControl
        {
            SpaceLabel = "Boşluk",
            DoneLabel = "Tamam",
            DoneCommand = new RelayCommand(() => doneInvoked = true),
        };
        var window = new Window { Width = 800, Height = 260, Content = keyboard };
        try
        {
            window.Show();
            Layout(window);

            // Starts shifted so first letter is uppercase Turkish 'Ç'
            Assert.True(keyboard.IsShiftActive);
            ClickKey(window, keyboard, "Ç");
            Assert.Equal("Ç", keyboard.Text);
            Assert.False(keyboard.IsShiftActive);

            // Next letters are lowercase Turkish characters
            ClickKey(window, keyboard, "a");
            ClickKey(window, keyboard, "ğ");
            ClickKey(window, keyboard, "ı");
            Assert.Equal("Çağrı".Replace("rı", "ı"), keyboard.Text); // "Çağı"

            // Space re-enables Shift for the next word
            ClickButtonByName(window, keyboard, "Space");
            Assert.Equal("Çağı ", keyboard.Text);
            Assert.True(keyboard.IsShiftActive);

            ClickKey(window, keyboard, "İ");
            Assert.Equal("Çağı İ", keyboard.Text);

            // Backspace deletes last char
            ClickButtonByName(window, keyboard, "Backspace");
            Assert.Equal("Çağı ", keyboard.Text);
            Assert.True(keyboard.IsShiftActive);

            // Numeric mode toggle
            ClickKey(window, keyboard, "123");
            Assert.True(keyboard.IsSymbolsMode);
            ClickKey(window, keyboard, "7");
            Assert.Equal("Çağı 7", keyboard.Text);

            // Done button executes DoneCommand
            ClickButtonByName(window, keyboard, "Done");
            Assert.True(doneInvoked);
        }
        finally
        {
            window.Close();
        }
    }

    private static void ClickKey(Window window, VirtualKeyboardControl keyboard, string label)
    {
        var button = keyboard.GetVisualDescendants().OfType<Button>()
            .First(b => b.IsEffectivelyVisible && string.Equals(b.Content?.ToString(), label, StringComparison.Ordinal));
        Click(window, button);
    }

    private static void ClickButtonByName(Window window, VirtualKeyboardControl keyboard, string name)
    {
        var button = keyboard.GetVisualDescendants().OfType<Button>()
            .First(b => b.IsEffectivelyVisible && AutomationProperties.GetName(b) == name);
        Click(window, button);
    }

    private static void Click(Window window, Button button)
    {
        var position = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(position, MouseButton.Left);
        window.MouseUp(position, MouseButton.Left);
        Layout(window);
    }

    private static void Layout(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }
}
