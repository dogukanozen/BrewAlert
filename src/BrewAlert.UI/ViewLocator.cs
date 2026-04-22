using Avalonia.Controls;
using Avalonia.Controls.Templates;
using BrewAlert.UI.ViewModels;
using System;
using System.Diagnostics.CodeAnalysis;

namespace BrewAlert.UI;

/// <summary>
/// Given a view model, returns the corresponding view using naming convention.
/// </summary>
[RequiresUnreferencedCode("ViewLocator uses reflection for view discovery.")]
public sealed class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var name = param.GetType().FullName!
            .Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type is not null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
