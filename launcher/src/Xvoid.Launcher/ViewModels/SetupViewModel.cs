using System.Collections.ObjectModel;
using Xvoid.Launcher.Core;

namespace Xvoid.Launcher.ViewModels;

/// <summary>The first-run steps: what this is, the game, the PC, the engine, the preset.</summary>
public sealed class SetupViewModel : ObservableObject
{
    public const int Steps = 5;
    readonly AppModel _app;
    int _step;

    public SetupViewModel(AppModel app, SettingsViewModel settings, EngineViewModel engine)
    {
        _app = app;
        Settings = settings;
        Engine = engine;
        NextCommand = new RelayCommand(Next, () => CanContinue);
        BackCommand = new RelayCommand(() => Step--, () => Step > 0);
        settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SettingsViewModel.GamePath) or nameof(SettingsViewModel.GameProblem)) Refresh();
        };
    }

    public SettingsViewModel Settings { get; }
    public EngineViewModel Engine { get; }
    public RelayCommand NextCommand { get; }
    public RelayCommand BackCommand { get; }
    public event Action? Finished;

    public int Step
    {
        get => _step;
        set
        {
            if (!Set(ref _step, Math.Clamp(value, 0, Steps - 1))) return;
            Refresh();
            if (_step == 2) Settings.RecheckCommand.Execute(null);
        }
    }

    public bool IsStep0 => Step == 0;
    public bool IsStep1 => Step == 1;
    public bool IsStep2 => Step == 2;
    public bool IsStep3 => Step == 3;
    public bool IsStep4 => Step == 4;
    public string StepText => $"Step {Step + 1} of {Steps}";
    public string NextText => Step == Steps - 1 ? "Finish" : "Continue";

    /// <summary>Each step says what it still needs; the others can always continue.</summary>
    public bool CanContinue => Step switch
    {
        1 => _app.Settings.GameExe is not null && GameLocator.Validate(_app.Settings.GameExe) is null,
        2 => !Checks.Blocking(_app.Checks),
        3 => _app.EnginePath is not null,
        _ => true,
    };

    public string? Blocker => Step switch
    {
        1 when !CanContinue => "Choose the folder that holds your game's files.",
        2 when !CanContinue => "This PC does not meet the minimum requirements.",
        3 when !CanContinue => "Install the engine, or choose a local build.",
        _ => null,
    };

    void Next()
    {
        if (Step == Steps - 1)
        {
            _app.Settings.SetupDone = true;
            _app.Save();
            Finished?.Invoke();
            return;
        }
        Step++;
    }

    public void Refresh()
    {
        foreach (var n in new[] { nameof(IsStep0), nameof(IsStep1), nameof(IsStep2), nameof(IsStep3), nameof(IsStep4), nameof(StepText), nameof(NextText), nameof(CanContinue), nameof(Blocker) })
            Raise(n);
        NextCommand.NotifyCanExecuteChanged();
        BackCommand.NotifyCanExecuteChanged();
    }
}
