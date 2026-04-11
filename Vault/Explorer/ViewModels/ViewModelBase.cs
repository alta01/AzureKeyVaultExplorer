using ReactiveUI;

namespace Microsoft.Vault.Explorer.ViewModels;

/// <summary>
/// Base class for all ViewModels in the Avalonia migration.
/// Inherits ReactiveObject for INotifyPropertyChanged and reactive property support.
/// </summary>
/// <remarks>
/// Usage patterns:
///   - Observable properties: use [Reactive] attribute (ReactiveUI.Fody) or
///     this.RaiseAndSetIfChanged(ref _field, value) in property setters.
///   - Commands: ReactiveCommand.CreateFromTask(() => DoWorkAsync(ct), canExecute)
///   - Side effects: this.WhenAnyValue(x => x.Prop).Subscribe(...)
///   - Thread safety: always use RxApp.MainThreadScheduler for UI mutations.
/// </remarks>
public abstract class ViewModelBase : ReactiveObject
{
}
