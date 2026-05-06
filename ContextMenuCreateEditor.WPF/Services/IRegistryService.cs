using System.Collections.Generic;
using ContextMenuCreateEditor.WPF.Models;

namespace ContextMenuCreateEditor.WPF.Services
{
    public enum AddOutcome
    {
        Created,
        Conflict,
        Updated
    }

    public class AddResult
    {
        public AddOutcome Outcome { get; init; }
        public string? ExistingProgId { get; init; }
        public ShellNewItem? Item { get; init; }
    }

    public interface IRegistryService
    {
        IReadOnlyList<ShellNewItem> GetItems();
        AddResult TryAdd(string displayName, string fileName, string extension, string templatePath);
        AddResult AddUsingExistingProgId(string displayName, string fileName, string extension, string templatePath, string existingProgId);
        AddResult ForceAdd(string displayName, string fileName, string extension, string templatePath);
        void Update(ShellNewItem original, ShellNewItem updated);
        void Remove(ShellNewItem item);
        bool ValidateExtension(string extension, out string normalized, out string? error);
        bool ValidateName(string name, out string? error);
    }
}
