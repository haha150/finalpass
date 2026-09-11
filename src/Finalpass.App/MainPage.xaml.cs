using System.ComponentModel;
using Finalpass.App.ViewModels;
using Finalpass.Core;
using Finalpass.Cryptography;
using Finalpass.Infrastructure;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;

namespace Finalpass.App;

// Disposable fields are released in MainPage_Unloaded; Page has no IDisposable lifecycle to hook into.
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable")]
public sealed partial class MainPage : Page
{
    private const double NavigationPaneMaximumWidth = 480;
    private const double LoginListMaximumWidth = 800;

    private readonly DispatcherTimer _autosaveTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer _autoLockTimer = new() { Interval = TimeSpan.FromSeconds(15) };
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private DateTimeOffset _lastActivityUtc = DateTimeOffset.UtcNow;
    private AppSettings _settings = new();
    private Task _settingsLoadTask = Task.CompletedTask;
    private ColumnDefinition? _resizingPaneColumn;
    private uint _resizingPointerId;
    private double _lastResizePointerX;
    private int _systemLockInProgress;

    public MainPageViewModel ViewModel { get; } = new();

    public MainPage()
    {
        DataContext = ViewModel;
        InitializeComponent();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.VaultChanged += ViewModel_VaultChanged;
        _autosaveTimer.Tick += AutosaveTimer_Tick;
        _autoLockTimer.Tick += AutoLockTimer_Tick;
        _autoLockTimer.Start();
        Unloaded += MainPage_Unloaded;
        UpdateVisualState();
        _settingsLoadTask = LoadSettingsAsync();
    }

    private void PaneSplitter_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not UIElement splitter)
        {
            return;
        }

        ColumnDefinition? paneColumn = ReferenceEquals(splitter, NavigationPaneSplitter)
            ? NavigationPaneColumn
            : ReferenceEquals(splitter, LoginListSplitter)
                ? LoginListColumn
                : null;
        if (paneColumn is null ||
            !e.GetCurrentPoint(splitter).Properties.IsLeftButtonPressed ||
            !splitter.CapturePointer(e.Pointer))
        {
            return;
        }

        _resizingPaneColumn = paneColumn;
        _resizingPointerId = e.Pointer.PointerId;
        _lastResizePointerX = e.GetCurrentPoint(VaultPanel).Position.X;
        _lastActivityUtc = DateTimeOffset.UtcNow;
        e.Handled = true;
    }

    private void PaneSplitter_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not UIElement splitter ||
            _resizingPaneColumn is null ||
            e.Pointer.PointerId != _resizingPointerId)
        {
            return;
        }

        var point = e.GetCurrentPoint(VaultPanel);
        if (!point.Properties.IsLeftButtonPressed)
        {
            splitter.ReleasePointerCapture(e.Pointer);
            ResetPaneResize();
            return;
        }

        double delta = point.Position.X - _lastResizePointerX;
        double maximumWidth = GetPaneMaximumWidth(_resizingPaneColumn);
        double width = Math.Clamp(
            _resizingPaneColumn.ActualWidth + delta,
            _resizingPaneColumn.MinWidth,
            maximumWidth);
        _resizingPaneColumn.Width = new GridLength(width, GridUnitType.Pixel);
        _lastResizePointerX = point.Position.X;
        _lastActivityUtc = DateTimeOffset.UtcNow;
        e.Handled = true;
    }

    private void PaneSplitter_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not UIElement splitter || e.Pointer.PointerId != _resizingPointerId)
        {
            return;
        }

        splitter.ReleasePointerCapture(e.Pointer);
        ResetPaneResize();
        e.Handled = true;
    }

    private void PaneSplitter_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerId == _resizingPointerId)
        {
            ResetPaneResize();
        }
    }

    private double GetPaneMaximumWidth(ColumnDefinition paneColumn)
    {
        double splitterWidth = NavigationPaneSplitterColumn.ActualWidth +
            LoginListSplitterColumn.ActualWidth;
        double availableWidth = ReferenceEquals(paneColumn, NavigationPaneColumn)
            ? VaultPanel.ActualWidth - LoginListColumn.ActualWidth - EditorPaneColumn.MinWidth - splitterWidth
            : VaultPanel.ActualWidth - NavigationPaneColumn.ActualWidth - EditorPaneColumn.MinWidth - splitterWidth;
        double configuredMaximum = ReferenceEquals(paneColumn, NavigationPaneColumn)
            ? NavigationPaneMaximumWidth
            : LoginListMaximumWidth;

        return Math.Max(paneColumn.MinWidth, Math.Min(configuredMaximum, availableWidth));
    }

    private void ResetPaneResize()
    {
        _resizingPaneColumn = null;
        _resizingPointerId = 0;
        _lastResizePointerX = 0;
    }

    private async void NewVault_Click(object sender, RoutedEventArgs e)
    {
        if (!await PrepareForVaultSwitchAsync())
        {
            return;
        }

        NewVaultInput? input = await PromptForNewVaultAsync();
        if (input is null)
        {
            return;
        }

        FileSavePicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = SanitizeFilename(input.VaultName),
        };
        picker.FileTypeChoices.Add("Finalpass vault", new List<string> { ".fpass" });
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
        StorageFile? file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return;
        }

        try
        {
            ViewModel.StatusText = "Creating encrypted vault…";
            VaultDocument document = VaultDocument.Create(input.VaultName);
            OpenedVault opened = await Task.Run(
                () => VaultFileService.CreateAsync(
                    file.Path,
                    document,
                    input.MasterPassword.AsMemory(),
                    replaceExisting: true));
            ViewModel.SetOpenedVault(opened);
            await RememberVaultAsync(opened.Path);
            UpdateVisualState();
        }
        catch (Exception exception)
        {
            await ShowErrorAsync("The vault could not be created", exception);
        }
    }

    private async void OpenVault_Click(object sender, RoutedEventArgs e)
    {
        FileOpenPicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            ViewMode = PickerViewMode.List,
        };
        picker.FileTypeFilter.Add(".fpass");
        picker.FileTypeFilter.Add(".bak");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
        StorageFile? file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        await OpenVaultPathAsync(file.Path);
    }

    public async Task OpenVaultPathAsync(string path)
    {
        if (!await PrepareForVaultSwitchAsync())
        {
            return;
        }

        string? password = await PromptForPasswordAsync("Unlock vault", "Enter the master password for this vault.");
        if (password is null)
        {
            return;
        }

        try
        {
            ViewModel.StatusText = "Unlocking vault…";
            OpenedVault opened;
            try
            {
                opened = await Task.Run(
                    () => VaultFileService.OpenAsync(path, password.AsMemory()));
            }
            catch (VaultConcurrencyException)
            {
                ContentDialog readOnlyDialog = new()
                {
                    XamlRoot = XamlRoot,
                    Title = "Vault already open",
                    Content = "Another Finalpass process is editing this vault. Open a read-only " +
                        "snapshot instead? You can view, search, copy, and back up entries, but cannot save changes.",
                    PrimaryButtonText = "Open read-only",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Primary,
                };
                if (await readOnlyDialog.ShowAsync() != ContentDialogResult.Primary)
                {
                    ViewModel.StatusText = "Open canceled because the vault is in use.";
                    return;
                }

                opened = await Task.Run(
                    () => VaultFileService.OpenReadOnlyAsync(path, password.AsMemory()));
            }

            ViewModel.SetOpenedVault(opened);
            await RememberVaultAsync(opened.Path);
            UpdateVisualState();
        }
        catch (Exception exception)
        {
            await ShowErrorAsync("The vault could not be opened", exception);
        }
    }

    private async void SaveVault_Click(object sender, RoutedEventArgs e) =>
        await SaveCurrentAsync();

    private async void BackupVault_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsVaultOpen)
        {
            return;
        }

        if (!ViewModel.ApplyEditor())
        {
            return;
        }

        FileSavePicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = $"{SanitizeFilename(ViewModel.VaultName)}-backup",
        };
        picker.FileTypeChoices.Add("Finalpass vault", new List<string> { ".fpass" });
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
        StorageFile? file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return;
        }

        try
        {
            await VaultFileService.BackUpAsAsync(
                ViewModel.RequireOpenedVault(),
                file.Path,
                replaceExisting: true);
            ViewModel.StatusText = $"Verified backup saved to {file.Path}.";
        }
        catch (Exception exception)
        {
            await ShowErrorAsync("The backup could not be created", exception);
        }
    }

    private async void ChangeMasterPassword_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsVaultOpen)
        {
            return;
        }

        MasterPasswordChange? change = await PromptForMasterPasswordChangeAsync();
        if (change is null)
        {
            return;
        }

        await _saveGate.WaitAsync();
        try
        {
            IsEnabled = false;
            ViewModel.StatusText = "Changing master password…";
            await VaultFileService.ChangeMasterPasswordAsync(
                ViewModel.RequireOpenedVault(),
                change.CurrentPassword.AsMemory(),
                change.NewPassword.AsMemory());
            ViewModel.MarkSaved();
            ViewModel.StatusText = "Master password changed. The previous encrypted file is retained as .bak.";
        }
        catch (Exception exception)
        {
            await ShowErrorAsync("The master password could not be changed", exception);
        }
        finally
        {
            IsEnabled = true;
            _saveGate.Release();
        }
    }

    private async void LockVault_Click(object sender, RoutedEventArgs e)
    {
        if (!await PrepareForVaultSwitchAsync())
        {
            return;
        }

        ViewModel.CloseVault();
        await ClearClipboardSafelyAsync();
        UpdateVisualState();
    }

    private async void UnlockLastVault_Click(object sender, RoutedEventArgs e)
    {
        string? path = _settings.LastVaultPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            _settings.LastVaultPath = null;
            await SaveSettingsSafelyAsync();
            UpdateVisualState();
            ViewModel.StatusText = "The last-used vault is no longer available. Choose a vault to open.";
            return;
        }

        await OpenVaultPathAsync(path);
    }

    private void AddEntry_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AddEntry();
        UpdateVisualState();
    }

    private async void NewFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsVaultOpen)
        {
            return;
        }

        string? name = await PromptForFolderNameAsync("New folder", string.Empty);
        if (name is null)
        {
            return;
        }

        Guid? parentId = ViewModel.SelectedFolder?.Id;
        ViewModel.AddFolder(name, parentId);
    }

    private async void RenameFolder_Click(object sender, RoutedEventArgs e)
    {
        FolderListItem? folder = ViewModel.SelectedFolder;
        if (folder?.Id is not Guid folderId)
        {
            return;
        }

        string? name = await PromptForFolderNameAsync("Rename folder", folder.Name);
        if (name is null)
        {
            return;
        }

        ViewModel.RenameFolder(folderId, name);
    }

    private async void DeleteFolder_Click(object sender, RoutedEventArgs e)
    {
        FolderListItem? folder = ViewModel.SelectedFolder;
        if (folder?.Id is not Guid folderId)
        {
            return;
        }

        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = "Delete this folder?",
            Content = $"“{folder.Name}” will be removed. Its entries and child folders are kept " +
                "at the parent level when you save the vault.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            ViewModel.DeleteFolder(folderId);
        }
    }

    private async Task<string?> PromptForFolderNameAsync(string title, string initialName)
    {
        TextBox nameBox = new()
        {
            Header = "Folder name",
            Text = initialName,
            MaxLength = 256,
        };
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = nameBox,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        dialog.PrimaryButtonClick += (_, args) => args.Cancel = string.IsNullOrWhiteSpace(nameBox.Text);
        ContentDialogResult result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? nameBox.Text.Trim() : null;
    }

    private void ApplyEntry_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ApplyEditor())
        {
            ViewModel.StatusText = "Entry changes applied. Save the vault to persist them.";
        }

        UpdateVisualState();
    }

    private async void DeleteEntry_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedEntry is null)
        {
            return;
        }

        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = "Delete this entry?",
            Content = $"“{ViewModel.SelectedEntry.Title}” will be removed when you save the vault.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            ViewModel.DeleteSelected();
            UpdateVisualState();
        }
    }

    private async void GeneratePassword_Click(object sender, RoutedEventArgs e)
    {
        ComboBox modeBox = new() { Header = "Type", HorizontalAlignment = HorizontalAlignment.Stretch };
        modeBox.Items.Add(new ComboBoxItem { Content = "Password" });
        modeBox.Items.Add(new ComboBoxItem { Content = "Passphrase" });
        modeBox.SelectedIndex = 0;

        NumberBox lengthBox = new()
        {
            Header = "Password length",
            Minimum = 12,
            Maximum = 128,
            SmallChange = 1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Value = _settings.PasswordLength,
        };
        CheckBox uppercaseBox = new() { Content = "Uppercase", IsChecked = _settings.PasswordUppercase };
        CheckBox lowercaseBox = new() { Content = "Lowercase", IsChecked = _settings.PasswordLowercase };
        CheckBox digitsBox = new() { Content = "Digits", IsChecked = _settings.PasswordDigits };
        CheckBox symbolsBox = new() { Content = "Symbols", IsChecked = _settings.PasswordSymbols };
        CheckBox ambiguousBox = new()
        {
            Content = "Exclude ambiguous characters (I, l, 1, O, 0, o)",
            IsChecked = _settings.PasswordExcludeAmbiguous,
        };
        NumberBox wordsBox = new()
        {
            Header = "Passphrase words",
            Minimum = 6,
            Maximum = 16,
            SmallChange = 1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Value = _settings.PassphraseWordCount,
        };
        TextBox separatorBox = new()
        {
            Header = "Separator (punctuation only)",
            MaxLength = 3,
            Text = _settings.PassphraseSeparator,
        };
        CheckBox capitalizeBox = new()
        {
            Content = "Capitalize words",
            IsChecked = _settings.PassphraseCapitalizeWords,
        };
        CheckBox numberBox = new()
        {
            Content = "Append a four-digit number",
            IsChecked = _settings.PassphraseAppendNumber,
        };
        TextBlock validation = new()
        {
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red),
            TextWrapping = TextWrapping.Wrap,
        };
        StackPanel content = new() { Spacing = 10, MinWidth = 360 };
        content.Children.Add(modeBox);
        content.Children.Add(lengthBox);
        content.Children.Add(uppercaseBox);
        content.Children.Add(lowercaseBox);
        content.Children.Add(digitsBox);
        content.Children.Add(symbolsBox);
        content.Children.Add(ambiguousBox);
        content.Children.Add(wordsBox);
        content.Children.Add(separatorBox);
        content.Children.Add(capitalizeBox);
        content.Children.Add(numberBox);
        content.Children.Add(new TextBlock
        {
            Text = "Passphrases use an offline word list. Eight words provide more than 64 bits of selection entropy.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
        });
        content.Children.Add(validation);

        void UpdateMode()
        {
            bool passwordMode = modeBox.SelectedIndex == 0;
            lengthBox.Visibility = passwordMode ? Visibility.Visible : Visibility.Collapsed;
            uppercaseBox.Visibility = passwordMode ? Visibility.Visible : Visibility.Collapsed;
            lowercaseBox.Visibility = passwordMode ? Visibility.Visible : Visibility.Collapsed;
            digitsBox.Visibility = passwordMode ? Visibility.Visible : Visibility.Collapsed;
            symbolsBox.Visibility = passwordMode ? Visibility.Visible : Visibility.Collapsed;
            ambiguousBox.Visibility = passwordMode ? Visibility.Visible : Visibility.Collapsed;
            wordsBox.Visibility = passwordMode ? Visibility.Collapsed : Visibility.Visible;
            separatorBox.Visibility = passwordMode ? Visibility.Collapsed : Visibility.Visible;
            capitalizeBox.Visibility = passwordMode ? Visibility.Collapsed : Visibility.Visible;
            numberBox.Visibility = passwordMode ? Visibility.Collapsed : Visibility.Visible;
        }

        modeBox.SelectionChanged += (_, _) => UpdateMode();
        UpdateMode();
        string? generated = null;
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = "Generate a password",
            Content = new ScrollViewer { Content = content, MaxHeight = 580 },
            PrimaryButtonText = "Generate",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            try
            {
                if (modeBox.SelectedIndex == 0)
                {
                    PasswordGeneratorOptions options = new(
                        checked((int)lengthBox.Value),
                        uppercaseBox.IsChecked == true,
                        lowercaseBox.IsChecked == true,
                        digitsBox.IsChecked == true,
                        symbolsBox.IsChecked == true,
                        ambiguousBox.IsChecked == true);
                    generated = PasswordGenerator.Generate(options);
                    UpdatePasswordSettings(options);
                }
                else
                {
                    PassphraseGeneratorOptions options = new(
                        checked((int)wordsBox.Value),
                        separatorBox.Text,
                        capitalizeBox.IsChecked == true,
                        numberBox.IsChecked == true);
                    generated = PasswordGenerator.GeneratePassphrase(options);
                    UpdatePassphraseSettings(options);
                }
            }
            catch (Exception exception) when (exception is ArgumentException or OverflowException)
            {
                validation.Text = exception.Message;
                args.Cancel = true;
            }
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary && generated is not null)
        {
            ViewModel.Password = generated;
            ViewModel.StatusText = modeBox.SelectedIndex == 0
                ? "Generated a cryptographically random password."
                : "Generated a cryptographically random offline passphrase.";
            await SaveSettingsSafelyAsync();
        }
    }

    private void AddCustomField_Click(object sender, RoutedEventArgs e) =>
        ViewModel.AddCustomField();

    private void RemoveCustomField_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CustomFieldItem item })
        {
            ViewModel.RemoveCustomField(item);
        }
    }

    private void CopyUsername_Click(object sender, RoutedEventArgs e) =>
        CopyToClipboard(ViewModel.Username, "Username");

    private void CopyPassword_Click(object sender, RoutedEventArgs e) =>
        CopyToClipboard(ViewModel.Password, "Password");

    private void CopyEntryUsername_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: VaultEntry entry })
        {
            CopyToClipboard(entry.Username, "Username");
        }
    }

    private void CopyEntryPassword_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: VaultEntry entry })
        {
            CopyToClipboard(entry.Password, "Password");
        }
    }

    private async void OpenEntryWebsite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: VaultEntry entry })
        {
            await OpenWebsiteAsync(entry.Url);
        }
    }

    private async void OpenWebsite_Click(object sender, RoutedEventArgs e) =>
        await OpenWebsiteAsync(ViewModel.Url);

    private async Task OpenWebsiteAsync(string value)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            ViewModel.StatusText = "Enter a complete http:// or https:// website address first.";
            return;
        }

        try
        {
            bool launched = await Launcher.LaunchUriAsync(uri);
            ViewModel.StatusText = launched
                ? "Website opened in the default browser."
                : "Windows could not open the website.";
        }
        catch (Exception exception)
        {
            ViewModel.StatusText = $"Windows could not open the website: {exception.Message}";
        }
    }

    private void FilterChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FilterChipItem chip })
        {
            ViewModel.ClearFilter(chip.Kind);
        }
    }

    private void ClearFilters_Click(object sender, RoutedEventArgs e) =>
        ViewModel.ClearAllFilters();

    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        await _settingsLoadTask;

        NumberBox idleBox = new()
        {
            Header = "Lock after inactivity (minutes)",
            Minimum = 1,
            Maximum = 120,
            SmallChange = 1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Value = _settings.IdleLockMinutes,
        };
        NumberBox clipboardBox = new()
        {
            Header = "Clear copied values after (seconds)",
            Minimum = 5,
            Maximum = 300,
            SmallChange = 5,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Value = _settings.ClipboardClearSeconds,
        };
        StackPanel content = new() { Spacing = 12, MinWidth = 360 };
        content.Children.Add(idleBox);
        content.Children.Add(new TextBlock
        {
            Text = "The vault also locks when Windows locks or before the computer suspends.",
            TextWrapping = TextWrapping.Wrap,
        });
        content.Children.Add(clipboardBox);
        content.Children.Add(new TextBlock
        {
            Text = "These preferences and the last-used vault path contain no vault data or secrets. " +
                "They are stored for the current Windows user.",
            TextWrapping = TextWrapping.Wrap,
        });
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = "Settings",
            Content = content,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        if (double.IsNaN(idleBox.Value) || double.IsNaN(clipboardBox.Value))
        {
            ViewModel.StatusText = "Enter valid numeric security settings.";
            return;
        }

        _settings.IdleLockMinutes = checked((int)idleBox.Value);
        _settings.ClipboardClearSeconds = checked((int)clipboardBox.Value);
        _lastActivityUtc = DateTimeOffset.UtcNow;
        if (await SaveSettingsSafelyAsync())
        {
            ViewModel.StatusText = "Settings saved for this Windows user.";
        }
    }

    private async Task<bool> SaveCurrentAsync()
    {
        if (ViewModel.IsReadOnly)
        {
            ViewModel.StatusText = "This vault is read-only and cannot be saved.";
            return false;
        }

        _autosaveTimer.Stop();
        await _saveGate.WaitAsync();
        try
        {
            if (!ViewModel.IsVaultOpen || !ViewModel.ApplyEditor())
            {
                return false;
            }

            IsEnabled = false;
            ViewModel.StatusText = "Saving encrypted vault…";
            await VaultFileService.SaveAsync(ViewModel.RequireOpenedVault());
            ViewModel.MarkSaved();
            return true;
        }
        catch (Exception exception)
        {
            await ShowErrorAsync("The vault could not be saved", exception);
            return false;
        }
        finally
        {
            IsEnabled = true;
            _saveGate.Release();
        }
    }

    public async Task<bool> PrepareToCloseAsync()
    {
        bool canClose = await PrepareForVaultSwitchAsync();
        if (canClose)
        {
            ViewModel.CloseVault();
            await ClearClipboardSafelyAsync();
        }

        return canClose;
    }

    private async Task<bool> PrepareForVaultSwitchAsync()
    {
        if (!ViewModel.IsVaultOpen)
        {
            return true;
        }

        if (!ViewModel.IsDirty)
        {
            return true;
        }

        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = "Save changes?",
            Content = "The current vault contains unsaved changes.",
            PrimaryButtonText = "Save",
            SecondaryButtonText = "Discard",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            if (!await SaveCurrentAsync())
            {
                return false;
            }
        }
        else if (result != ContentDialogResult.Secondary)
        {
            return false;
        }

        return true;
    }

    private async Task<NewVaultInput?> PromptForNewVaultAsync()
    {
        TextBox nameBox = new() { Header = "Vault name", Text = "Personal", MaxLength = 256 };
        PasswordBox passwordBox = new() { Header = "Master password", MaxLength = 256 };
        PasswordBox confirmationBox = new() { Header = "Confirm master password", MaxLength = 256 };
        TextBlock validation = new()
        {
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red),
            TextWrapping = TextWrapping.Wrap,
        };
        StackPanel content = new() { Spacing = 12 };
        content.Children.Add(new TextBlock
        {
            Text = "Use a long, unique passphrase. It cannot be recovered if forgotten.",
            TextWrapping = TextWrapping.Wrap,
        });
        content.Children.Add(nameBox);
        content.Children.Add(passwordBox);
        content.Children.Add(confirmationBox);
        content.Children.Add(validation);

        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = "Create a vault",
            Content = content,
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (string.IsNullOrWhiteSpace(nameBox.Text))
            {
                validation.Text = "Enter a vault name.";
                args.Cancel = true;
            }
            else if (passwordBox.Password.Length == 0)
            {
                validation.Text = "Enter a master password.";
                args.Cancel = true;
            }
            else if (!string.Equals(passwordBox.Password, confirmationBox.Password, StringComparison.Ordinal))
            {
                validation.Text = "The master passwords do not match.";
                args.Cancel = true;
            }
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        NewVaultInput input = new(nameBox.Text.Trim(), passwordBox.Password);
        passwordBox.Password = string.Empty;
        confirmationBox.Password = string.Empty;
        return input;
    }

    private async Task<string?> PromptForPasswordAsync(string title, string message)
    {
        PasswordBox passwordBox = new() { Header = "Master password", MaxLength = 256 };
        StackPanel content = new() { Spacing = 12 };
        content.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        content.Children.Add(passwordBox);
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = content,
            PrimaryButtonText = "Unlock",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        dialog.PrimaryButtonClick += (_, args) => args.Cancel = passwordBox.Password.Length == 0;
        ContentDialogResult result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        string password = passwordBox.Password;
        passwordBox.Password = string.Empty;
        return password;
    }

    private async Task<MasterPasswordChange?> PromptForMasterPasswordChangeAsync()
    {
        PasswordBox currentBox = new() { Header = "Current master password", MaxLength = 256 };
        PasswordBox newBox = new() { Header = "New master password", MaxLength = 256 };
        PasswordBox confirmationBox = new() { Header = "Confirm new master password", MaxLength = 256 };
        TextBlock validation = new()
        {
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red),
            TextWrapping = TextWrapping.Wrap,
        };
        StackPanel content = new() { Spacing = 12 };
        content.Children.Add(new TextBlock
        {
            Text = "Use a long, unique passphrase. There is no password recovery.",
            TextWrapping = TextWrapping.Wrap,
        });
        content.Children.Add(currentBox);
        content.Children.Add(newBox);
        content.Children.Add(confirmationBox);
        content.Children.Add(validation);

        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = "Change master password",
            Content = content,
            PrimaryButtonText = "Change",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (currentBox.Password.Length == 0 || newBox.Password.Length == 0)
            {
                validation.Text = "Enter both the current and new master passwords.";
                args.Cancel = true;
            }
            else if (!string.Equals(newBox.Password, confirmationBox.Password, StringComparison.Ordinal))
            {
                validation.Text = "The new master passwords do not match.";
                args.Cancel = true;
            }
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        MasterPasswordChange change = new(currentBox.Password, newBox.Password);
        currentBox.Password = string.Empty;
        newBox.Password = string.Empty;
        confirmationBox.Password = string.Empty;
        return change;
    }

    private void CopyToClipboard(string value, string label)
    {
        if (string.IsNullOrEmpty(value))
        {
            ViewModel.StatusText = $"{label} is empty.";
            return;
        }

        try
        {
            TimeSpan clearAfter = TimeSpan.FromSeconds(_settings.ClipboardClearSeconds);
            SecureClipboard.Copy(value, clearAfter);
            ViewModel.StatusText = $"{label} copied. It will be cleared after " +
                $"{_settings.ClipboardClearSeconds} seconds if unchanged.";
        }
        catch (Exception exception)
        {
            ViewModel.StatusText = $"Clipboard error: {exception.Message}";
        }
    }

    private async Task ShowErrorAsync(string title, Exception exception)
    {
        ViewModel.StatusText = title + ".";
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = exception.Message,
            CloseButtonText = "OK",
        };
        await dialog.ShowAsync();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainPageViewModel.SelectedEntry)
            or nameof(MainPageViewModel.IsVaultOpen)
            or nameof(MainPageViewModel.SelectedFolder))
        {
            UpdateVisualState();
        }
    }

    private void ViewModel_VaultChanged(object? sender, EventArgs e)
    {
        _autosaveTimer.Stop();
        _autosaveTimer.Start();
        ViewModel.StatusText = "Unsaved changes…";
    }

    private async void AutosaveTimer_Tick(object? sender, object e)
    {
        _autosaveTimer.Stop();
        if (ViewModel.IsVaultOpen && ViewModel.IsDirty)
        {
            await SaveCurrentAsync();
        }
    }

    private async void AutoLockTimer_Tick(object? sender, object e)
    {
        if (!ViewModel.IsVaultOpen ||
            DateTimeOffset.UtcNow - _lastActivityUtc < TimeSpan.FromMinutes(_settings.IdleLockMinutes))
        {
            return;
        }

        _autoLockTimer.Stop();
        if (ViewModel.IsDirty && !await SaveCurrentAsync())
        {
            _lastActivityUtc = DateTimeOffset.UtcNow;
            _autoLockTimer.Start();
            return;
        }

        ViewModel.CloseVault();
        await ClearClipboardSafelyAsync();
        UpdateVisualState();
        ViewModel.StatusText = $"Vault locked after {_settings.IdleLockMinutes} minutes of inactivity.";
        _autoLockTimer.Start();
    }

    private void Page_PointerMoved(object sender, PointerRoutedEventArgs e) =>
        _lastActivityUtc = DateTimeOffset.UtcNow;

    private void Page_KeyDown(object sender, KeyRoutedEventArgs e) =>
        _lastActivityUtc = DateTimeOffset.UtcNow;

    private async void SaveAccelerator_Invoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (ViewModel.IsWritable)
        {
            await SaveCurrentAsync();
        }
    }

    private void OpenAccelerator_Invoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        OpenVault_Click(sender, new RoutedEventArgs());
    }

    private void NewVaultAccelerator_Invoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        NewVault_Click(sender, new RoutedEventArgs());
    }

    private void LockAccelerator_Invoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (ViewModel.IsVaultOpen)
        {
            LockVault_Click(sender, new RoutedEventArgs());
        }
    }

    private void SearchAccelerator_Invoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (ViewModel.IsVaultOpen)
        {
            SearchBox.Focus(FocusState.Keyboard);
        }
    }

    private void CopyUsernameAccelerator_Invoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (ViewModel.SelectedEntry is not null)
        {
            CopyToClipboard(ViewModel.Username, "Username");
        }
    }

    private void CopyPasswordAccelerator_Invoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (ViewModel.SelectedEntry is not null)
        {
            CopyToClipboard(ViewModel.Password, "Password");
        }
    }

    public async Task LockForSystemEventAsync(string reason)
    {
        if (!ViewModel.IsVaultOpen || Interlocked.Exchange(ref _systemLockInProgress, 1) != 0)
        {
            return;
        }

        try
        {
            _autosaveTimer.Stop();
            if (ViewModel.IsWritable && ViewModel.IsDirty)
            {
                await _saveGate.WaitAsync();
                try
                {
                    if (ViewModel.ApplyEditor())
                    {
                        await VaultFileService.SaveAsync(ViewModel.RequireOpenedVault());
                        ViewModel.MarkSaved();
                    }
                }
                catch
                {
                    // System lock and suspend must discard decrypted state even if
                    // the last autosave cannot complete. The previous encrypted
                    // vault remains protected by the atomic save path.
                }
                finally
                {
                    _saveGate.Release();
                }
            }

            ViewModel.CloseVault();
            await ClearClipboardSafelyAsync();
            UpdateVisualState();
            ViewModel.StatusText = $"Vault locked because Windows {reason}.";
        }
        finally
        {
            Interlocked.Exchange(ref _systemLockInProgress, 0);
        }
    }

    private async Task LoadSettingsAsync()
    {
        _settings = await AppSettingsService.LoadAsync();
        if (!string.IsNullOrWhiteSpace(_settings.LastVaultPath) &&
            !File.Exists(_settings.LastVaultPath))
        {
            _settings.LastVaultPath = null;
            await SaveSettingsSafelyAsync();
        }

        UpdateVisualState();
    }

    private async Task RememberVaultAsync(string path)
    {
        await _settingsLoadTask;
        _settings.LastVaultPath = Path.GetFullPath(path);
        await SaveSettingsSafelyAsync();
    }

    private async Task<bool> SaveSettingsSafelyAsync()
    {
        try
        {
            await AppSettingsService.SaveAsync(_settings);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ViewModel.StatusText = "The preferences could not be saved; current-session values remain active.";
            return false;
        }
    }

    private void UpdatePasswordSettings(PasswordGeneratorOptions options)
    {
        _settings.PasswordLength = options.Length;
        _settings.PasswordUppercase = options.IncludeUppercase;
        _settings.PasswordLowercase = options.IncludeLowercase;
        _settings.PasswordDigits = options.IncludeDigits;
        _settings.PasswordSymbols = options.IncludeSymbols;
        _settings.PasswordExcludeAmbiguous = options.ExcludeAmbiguous;
    }

    private void UpdatePassphraseSettings(PassphraseGeneratorOptions options)
    {
        _settings.PassphraseWordCount = options.WordCount;
        _settings.PassphraseSeparator = options.Separator;
        _settings.PassphraseCapitalizeWords = options.CapitalizeWords;
        _settings.PassphraseAppendNumber = options.AppendNumber;
    }

    private async void MainPage_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        ViewModel.VaultChanged -= ViewModel_VaultChanged;
        _autosaveTimer.Stop();
        _autosaveTimer.Tick -= AutosaveTimer_Tick;
        _autoLockTimer.Stop();
        _autoLockTimer.Tick -= AutoLockTimer_Tick;
        ViewModel.Dispose();
        _saveGate.Dispose();
        await ClearClipboardSafelyAsync();
    }

    private void UpdateVisualState()
    {
        bool isOpen = ViewModel.IsVaultOpen;
        bool isWritable = ViewModel.IsWritable;
        WelcomePanel.Visibility = isOpen ? Visibility.Collapsed : Visibility.Visible;
        VaultPanel.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
        SaveButton.IsEnabled = isWritable;
        SaveButton.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
        BackupButton.IsEnabled = isOpen;
        BackupButton.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
        ChangePasswordButton.IsEnabled = isWritable;
        ChangePasswordButton.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
        VaultMenuSeparator.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
        LockButton.IsEnabled = isOpen;
        LockButton.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
        AddEntryButton.IsEnabled = isWritable;
        NewFolderButton.IsEnabled = isWritable;
        bool hasSelection = ViewModel.SelectedEntry is not null;
        EditorPanel.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
        NoSelectionText.Visibility = hasSelection ? Visibility.Collapsed : Visibility.Visible;
        bool hasRealFolderSelected = ViewModel.SelectedFolder?.Id is not null;
        RenameFolderButton.IsEnabled = isWritable && hasRealFolderSelected;
        DeleteFolderButton.IsEnabled = isWritable && hasRealFolderSelected;

        bool hasRecentVault = !string.IsNullOrWhiteSpace(_settings.LastVaultPath) &&
            File.Exists(_settings.LastVaultPath);
        UnlockLastVaultButton.Visibility = !isOpen && hasRecentVault
            ? Visibility.Visible
            : Visibility.Collapsed;
        LastVaultDescription.Visibility = !isOpen && hasRecentVault
            ? Visibility.Visible
            : Visibility.Collapsed;
        CreateVaultButton.Visibility = !isOpen && !hasRecentVault
            ? Visibility.Visible
            : Visibility.Collapsed;
        LastVaultDescription.Text = hasRecentVault
            ? $"Last used: {Path.GetFileName(_settings.LastVaultPath)}"
            : string.Empty;
    }

    private static string SanitizeFilename(string value)
    {
        string result = string.Join("-", value.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(result) ? "vault" : result;
    }

    private static async Task ClearClipboardSafelyAsync()
    {
        try
        {
            await SecureClipboard.ClearIfOwnedAsync();
        }
        catch
        {
            // Clipboard access can fail when Windows is shutting down.
        }
    }

    private sealed record NewVaultInput(string VaultName, string MasterPassword);

    private sealed record MasterPasswordChange(string CurrentPassword, string NewPassword);
}
