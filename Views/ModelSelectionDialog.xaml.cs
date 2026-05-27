using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Verdict.Models;

namespace Verdict.Views;

/// <summary>
/// Dialog for selecting a model from a structured list with name, source, size, and architecture columns.
/// Supports search/filter and entering a HuggingFace model ID to download.
/// </summary>
public partial class ModelSelectionDialog : Window
{
    private List<ModelListItem> _allModels = new();
    public string SelectedModelId { get; private set; } = string.Empty;
    
    public ModelSelectionDialog(List<ModelListItem> availableModels)
    {
        InitializeComponent();
        _allModels = availableModels;
        ModelList.ItemsSource = availableModels;
        Title = "Select Model";
        SearchBox.Focus();
    }

    /// <summary>
    /// Backward-compatible constructor for string-based model lists.
    /// </summary>
    public ModelSelectionDialog(List<string> availableModelStrings, bool _)
    {
        InitializeComponent();
        var items = new List<ModelListItem>();
        foreach (var s in availableModelStrings)
        {
            items.Add(new ModelListItem { DisplayName = s, ModelId = s, Source = "Remote", IsHeader = s.StartsWith("---") || s.StartsWith("⚠") });
        }
        _allModels = items;
        ModelList.ItemsSource = items;
        Title = "Select Model";
        SearchBox.Focus();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = SearchBox.Text.Trim();
        var filtered = new List<ModelListItem>();

        if (string.IsNullOrWhiteSpace(searchText))
        {
            // Show all models
            ModelList.ItemsSource = _allModels;
            return;
        }

        // If the search looks like a HuggingFace model ID, offer download option first
        if (searchText.Contains("/"))
        {
            filtered.Add(new ModelListItem
            {
                DisplayName = $"Download: {searchText}",
                ModelId = searchText,
                Source = "Download",
                IsHeader = false
            });
            filtered.Add(new ModelListItem { DisplayName = "─── Available Models ───", IsHeader = true });
        }

        // Filter existing models by search text
        var lower = searchText.ToLowerInvariant();
        foreach (var model in _allModels.Where(m => !m.IsHeader))
        {
            if (model.DisplayName.ToLowerInvariant().Contains(lower) ||
                model.ModelId.ToLowerInvariant().Contains(lower) ||
                model.Source.ToLowerInvariant().Contains(lower) ||
                model.Architecture.ToLowerInvariant().Contains(lower))
            {
                filtered.Add(model);
            }
        }

        ModelList.ItemsSource = filtered;
    }
    
    private void SelectModel_Click(object sender, RoutedEventArgs e)
    {
        if (ModelList.SelectedItem is ModelListItem item && !item.IsHeader)
        {
            SelectedModelId = string.IsNullOrWhiteSpace(item.ModelId) ? item.DisplayName : item.ModelId;
            DialogResult = true;
        }
        else if (ModelList.SelectedItem is ModelListItem header && header.IsHeader)
        {
            // Don't allow selecting header rows
        }
        else
        {
            MessageBox.Show("Please select a model or enter a HuggingFace model ID to download.");
        }
    }

    private void ModelList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ModelList.SelectedItem is ModelListItem item && !item.IsHeader)
        {
            SelectedModelId = string.IsNullOrWhiteSpace(item.ModelId) ? item.DisplayName : item.ModelId;
            DialogResult = true;
        }
    }
    
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}

/// <summary>
/// Applies a disabled header style to ListView items where IsHeader is true.
/// </summary>
public class HeaderRowStyleSelector : StyleSelector
{
    public Style HeaderStyle { get; set; } = null!;

    public override Style SelectStyle(object item, DependencyObject container)
    {
        if (item is ModelListItem mi && mi.IsHeader)
            return HeaderStyle;
        return base.SelectStyle(item, container);
    }
}