namespace ContextMenuCreateEditor.WPF.Models
{
    public class ShellNewItem
    {
        public string DisplayName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public string Template { get; set; } = string.Empty;
        public string? TemplatePath { get; set; }
        public string? ProgId { get; set; }
        public bool IsOwn { get; set; }
        public bool UseCommandMode { get; set; }

        public ShellNewItem Clone() => new()
        {
            DisplayName = DisplayName,
            FileName = FileName,
            Extension = Extension,
            Template = Template,
            TemplatePath = TemplatePath,
            ProgId = ProgId,
            IsOwn = IsOwn,
            UseCommandMode = UseCommandMode
        };
    }
}
