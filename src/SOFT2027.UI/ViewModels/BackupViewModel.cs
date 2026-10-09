using System;
using System.Threading.Tasks;
using SOFT2027.Core.Interfaces;

namespace SOFT2027.Presentation.ViewModels;

public sealed class BackupViewModel : ViewModelBase
{
    private readonly IDatabaseBackupService _backupService;
    private bool _isBusy;
    private string _lastBackup = "No backup created this session.";

    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string LastBackup { get => _lastBackup; private set => SetProperty(ref _lastBackup, value); }

    public BackupViewModel(IDatabaseBackupService backupService) =>
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));

    public async Task CreateBackupAsync(string path)
    {
        try
        {
            IsBusy = true;
            ClearErrors();
            await _backupService.CreateBackupAsync(path);
            LastBackup = path;
            Announce("Backup created and ready to copy to a safe location.");
        }
        catch (Exception ex) { SetError($"Backup was not created. {ex.Message}"); }
        finally { IsBusy = false; }
    }

    public async Task RestoreBackupAsync(string path)
    {
        try
        {
            IsBusy = true;
            ClearErrors();
            var safetyCopy = await _backupService.RestoreBackupAsync(path);
            LastBackup = safetyCopy;
            Announce("Backup restored. Your previous data is saved in the safety copy shown below. Reopen a page to refresh its data.");
        }
        catch (Exception ex) { SetError($"Backup could not be restored. {ex.Message}"); }
        finally { IsBusy = false; }
    }
}
