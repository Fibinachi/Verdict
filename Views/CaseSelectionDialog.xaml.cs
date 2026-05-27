using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using Verdict.Models;

namespace Verdict.Views;

public partial class CaseSelectionDialog : Window
{
    public string? SelectedFilePath { get; private set; }

    private class CaseItem
    {
        public string FileName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Description { get; set; } = "";
        public string CaseType { get; set; } = "";
    }

    public CaseSelectionDialog()
    {
        InitializeComponent();
        LoadCases();
    }

    private void LoadCases()
    {
        var casesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "DefaultCases");

        // Also check next to the EXE
        if (!Directory.Exists(casesDir))
            casesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DefaultCases");

        if (!Directory.Exists(casesDir))
        {
            CaseList.ItemsSource = new List<CaseItem>
            {
                new() { DisplayName = "No demo cases found", Description = "DefaultCases folder not found", CaseType = "" }
            };
            return;
        }

        var items = new List<CaseItem>();
        foreach (var file in Directory.GetFiles(casesDir, "*.jur"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var caseFile = JsonSerializer.Deserialize<CaseFile>(json, opts);

                if (caseFile != null)
                {
                    items.Add(new CaseItem
                    {
                        FileName = file,
                        DisplayName = caseFile.CaseName ?? Path.GetFileNameWithoutExtension(file),
                        Description = BuildDescription(caseFile),
                        CaseType = $"{caseFile.Mode} | {caseFile.Jurisdiction} | {caseFile.CourtName ?? "Superior Court"}"
                    });
                }
            }
            catch
            {
                // Skip unparseable files
            }
        }

        CaseList.ItemsSource = items;
        if (items.Count > 0)
            CaseList.SelectedIndex = 0;
    }

    private static string BuildDescription(CaseFile cf)
    {
        var parts = new List<string>();
        if (cf.Plaintiffs.Count > 0 && cf.Defendants.Count > 0)
            parts.Add($"{cf.Plaintiffs[0]} v. {cf.Defendants[0]}");
        if (cf.Evidence.Count > 0)
            parts.Add($"{cf.Evidence.Count} evidence items");
        if (cf.Verdict?.Charges.Count > 0)
            parts.Add($"{cf.Verdict.Charges.Count} charges");
        if (cf.Verdict?.CausesOfAction.Count > 0)
            parts.Add($"{cf.Verdict.CausesOfAction.Count} causes of action");
        return parts.Count > 0 ? string.Join(" | ", parts) : "No case details available";
    }

    private void OpenSelected_Click(object sender, RoutedEventArgs e)
    {
        if (CaseList.SelectedItem is CaseItem item && !string.IsNullOrEmpty(item.FileName))
        {
            SelectedFilePath = item.FileName;
            DialogResult = true;
        }
        else
        {
            MessageBox.Show("Please select a case to open.", "No Selection");
        }
    }

    private void BlankCase_Click(object sender, RoutedEventArgs e)
    {
        // Signal that a blank case is desired by leaving SelectedFilePath null
        SelectedFilePath = null;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
