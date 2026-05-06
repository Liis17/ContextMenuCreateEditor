using System.Threading.Tasks;

namespace ContextMenuCreateEditor.WPF.Services
{
    public interface IExplorerRefresher
    {
        void RefreshAssociations();
        Task RestartShellAsync();
    }
}
