namespace ContextMenuCreateEditor.WPF.Services
{
    public interface ITemplateStorage
    {
        string TemplatesRoot { get; }
        string SaveTemplate(string fileName, string extension, string content);
        bool DeleteTemplate(string? path);
        string? ReadTemplate(string? path);
    }
}
