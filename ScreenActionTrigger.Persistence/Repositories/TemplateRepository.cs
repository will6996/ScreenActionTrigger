using Microsoft.Extensions.Logging;
using ScreenActionTrigger.Core.Interfaces;
using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.Persistence.Repositories;

public sealed class TemplateRepository : ITemplateLibrary
{
    private readonly ILogger<TemplateRepository> _logger;
    private readonly List<Template> _templates = new();
    private readonly string _templateDir;

    public IReadOnlyList<Template> Templates => _templates.AsReadOnly();

    public IReadOnlyList<string> Categories => _templates
        .Select(t => t.Category)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(c => c)
        .ToList()
        .AsReadOnly();

    public TemplateRepository(ILogger<TemplateRepository> logger)
    {
        _logger = logger;
        // Compatível com single-file: caminhos baseados em %APPDATA%, não no exe
        _templateDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ScreenActionTrigger", "Templates");
        Directory.CreateDirectory(_templateDir);
        SeedDefaultCategories();
    }

    private void SeedDefaultCategories() { }

    public void SetTemplates(IEnumerable<Template> templates)
    {
        _templates.Clear();
        _templates.AddRange(templates);
    }

    public void Add(Template template)
    {
        _templates.RemoveAll(t => t.Id == template.Id);
        _templates.Add(template);
    }

    public void Remove(Guid templateId)
    {
        var t = GetById(templateId);
        if (t is not null && File.Exists(t.ImagePath))
        {
            try { File.Delete(t.ImagePath); } catch { }
        }
        _templates.RemoveAll(t => t.Id == templateId);
    }

    public void Update(Template template)
    {
        var idx = _templates.FindIndex(t => t.Id == template.Id);
        if (idx >= 0) _templates[idx] = template;
        else _templates.Add(template);
    }

    public Template? GetById(Guid id) => _templates.FirstOrDefault(t => t.Id == id);

    public IEnumerable<Template> GetByCategory(string category)
        => _templates.Where(t => string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase));

    public Template Duplicate(Guid templateId)
    {
        var original = GetById(templateId) ?? throw new KeyNotFoundException($"Template {templateId} not found");
        var clone = original.Clone();

        // Copy image file
        if (File.Exists(original.ImagePath))
        {
            var ext  = Path.GetExtension(original.ImagePath);
            var dest = Path.Combine(_templateDir, $"{clone.Id}{ext}");
            File.Copy(original.ImagePath, dest, true);
            clone.ImagePath = dest;
        }

        _templates.Add(clone);
        return clone;
    }

    public async Task<Template> ImportFromFileAsync(string filePath, string category = "Personalizados")
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException("Image not found", filePath);

        var ext = Path.GetExtension(filePath).ToLower();
        if (ext is not ".png" and not ".jpg" and not ".jpeg" and not ".bmp")
            throw new ArgumentException("Unsupported image format. Use PNG or JPG.", nameof(filePath));

        var template = new Template
        {
            Name = Path.GetFileNameWithoutExtension(filePath),
            Category = category
        };

        var destPath = Path.Combine(_templateDir, $"{template.Id}{ext}");
        await Task.Run(() => File.Copy(filePath, destPath, true));
        template.ImagePath = destPath;

        _templates.Add(template);
        _logger.LogInformation("Template '{Name}' imported from {Path}", template.Name, filePath);
        return template;
    }

    public async Task ExportAsync(Template template, string destinationPath)
    {
        if (!File.Exists(template.ImagePath)) throw new FileNotFoundException("Template image not found");
        await Task.Run(() => File.Copy(template.ImagePath, destinationPath, true));
    }

    public async Task<byte[]?> LoadImageBytesAsync(Guid templateId)
    {
        var template = GetById(templateId);
        if (template is null || !File.Exists(template.ImagePath)) return null;
        return await File.ReadAllBytesAsync(template.ImagePath);
    }
}
