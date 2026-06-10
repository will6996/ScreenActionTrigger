using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ScreenActionTrigger.Core.Interfaces;
using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.UI.ViewModels;

public sealed partial class TemplatesViewModel : ObservableObject
{
    private readonly ITemplateLibrary _library;

    public ObservableCollection<Template> Templates { get; } = new();
    public ObservableCollection<string>   Categories { get; } = new();

    [ObservableProperty] private Template? _selectedTemplate;
    [ObservableProperty] private string    _filterCategory = "Todos";
    [ObservableProperty] private string    _filterText     = string.Empty;
    [ObservableProperty] private bool      _isCapturing;

    public static IEnumerable<MatchingMethod> AllMethods =>
        Enum.GetValues<MatchingMethod>();

    public TemplatesViewModel(ITemplateLibrary library) => _library = library;

    public void SetProfile(ExecutionProfile profile)
    {
        _library.SetTemplates(profile.Templates);
        RefreshAll();
    }

    private void RefreshAll()
    {
        Templates.Clear();
        foreach (var t in _library.Templates) Templates.Add(t);

        Categories.Clear();
        Categories.Add("Todos");
        Categories.Add("Interface");
        Categories.Add("Combate");
        Categories.Add("Recursos");
        Categories.Add("Eventos");
        Categories.Add("Personalizados");
        foreach (var c in _library.Categories.Where(c =>
            !new[] { "Interface","Combate","Recursos","Eventos","Personalizados" }
                .Contains(c, StringComparer.OrdinalIgnoreCase)))
            Categories.Add(c);
    }

    [RelayCommand]
    private async Task ImportTemplateAsync()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Imagens (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
            Title  = "Importar Template",
            Multiselect = true
        };
        if (dlg.ShowDialog() != true) return;

        foreach (var file in dlg.FileNames)
        {
            var t = await _library.ImportFromFileAsync(file,
                FilterCategory == "Todos" ? "Personalizados" : FilterCategory);
            Templates.Add(t);
        }
    }

    [RelayCommand]
    private void DuplicateTemplate(Template? template)
    {
        if (template is null) return;
        var clone = _library.Duplicate(template.Id);
        Templates.Add(clone);
        SelectedTemplate = clone;
    }

    [RelayCommand]
    private async Task ExportTemplateAsync(Template? template)
    {
        if (template is null) return;
        var dlg = new SaveFileDialog
        {
            FileName   = template.Name,
            DefaultExt = ".png",
            Filter     = "PNG (*.png)|*.png|JPEG (*.jpg)|*.jpg"
        };
        if (dlg.ShowDialog() != true) return;
        await _library.ExportAsync(template, dlg.FileName);
    }

    [RelayCommand]
    private void RemoveTemplate(Template? template)
    {
        if (template is null) return;
        _library.Remove(template.Id);
        Templates.Remove(template);
        if (SelectedTemplate == template) SelectedTemplate = null;
    }

    [RelayCommand]
    private void CaptureFromScreen()
    {
        // Signal UI layer to enter capture mode
        IsCapturing = true;
        TemplateCaptureRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? TemplateCaptureRequested;

    public async Task ApplyCapturedImageAsync(byte[] imageData, string suggestedName)
    {
        IsCapturing = false;
        var dir   = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ScreenActionTrigger", "Templates");
        Directory.CreateDirectory(dir);

        var id   = Guid.NewGuid();
        var path = Path.Combine(dir, $"{id}.png");
        await File.WriteAllBytesAsync(path, imageData);

        var t = new Template
        {
            Id          = id,
            Name        = suggestedName,
            Category    = FilterCategory == "Todos" ? "Personalizados" : FilterCategory,
            ImagePath   = path
        };
        _library.Add(t);
        Templates.Add(t);
        SelectedTemplate = t;
    }

    public IEnumerable<Template> FilteredTemplates => Templates.Where(t =>
        (FilterCategory == "Todos" || string.Equals(t.Category, FilterCategory, StringComparison.OrdinalIgnoreCase)) &&
        (string.IsNullOrEmpty(FilterText) ||
         t.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase)));

    partial void OnFilterCategoryChanged(string value) => OnPropertyChanged(nameof(FilteredTemplates));
    partial void OnFilterTextChanged(string value)     => OnPropertyChanged(nameof(FilteredTemplates));
}
