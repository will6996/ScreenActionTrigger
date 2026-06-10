using ScreenActionTrigger.Core.Models;

namespace ScreenActionTrigger.Core.Interfaces;

public interface ITemplateLibrary
{
    IReadOnlyList<Template> Templates { get; }
    IReadOnlyList<string> Categories { get; }

    void Add(Template template);
    void Remove(Guid templateId);
    void Update(Template template);
    Template? GetById(Guid id);
    IEnumerable<Template> GetByCategory(string category);
    Template Duplicate(Guid templateId);

    Task<Template> ImportFromFileAsync(string filePath, string category = "Personalizados");
    Task ExportAsync(Template template, string destinationPath);
    Task<byte[]?> LoadImageBytesAsync(Guid templateId);

    void SetTemplates(IEnumerable<Template> templates);
}
