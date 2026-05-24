using System.Windows;
using DenoPlayer.Models;
using DenoPlayer.Services;
using DenoPlayer.ViewModels;

namespace DenoPlayer.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;
    private readonly AppSettings _settings;
    private readonly SettingsService _svc;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        _svc = new SettingsService();
        _vm = new SettingsViewModel(settings);
        DataContext = _vm;
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        try
        {
            var selected = _vm.SelectedExtensions.ToList();

            // 1) AppSettings 저장
            _settings.RegisteredExtensions = selected;
            _svc.Save(_settings);

            // 2) HKCU 레지스트리 갱신
            var exePath = System.IO.Path.Combine(
                AppContext.BaseDirectory, "DenoPlayer.exe");
            FileAssociationService.RegisterApplication(exePath);
            FileAssociationService.SyncExtensions(selected, _vm.AllKnownExtensions);

            // 3) 사용자에게 안내 + Windows 기본 앱 화면 열기
            var ans = MessageBox.Show(
                this,
                $"등록 완료 ({selected.Count}개 확장자).\n\n" +
                "이제 탐색기에서 우클릭 → '연결 프로그램' 메뉴에 Deno Player가 나옵니다.\n\n" +
                "지금 Windows '기본 앱' 설정을 열어 한 번에 기본 앱으로 지정할까요?",
                "Deno Player",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (ans == MessageBoxResult.Yes)
                FileAssociationService.OpenDefaultAppsSettings();

            Close();
        }
        catch (Exception ex)
        {
            AppLog.Error("Settings confirm", ex);
            MessageBox.Show(this, "등록 중 오류:\n" + ex.Message, "Deno Player",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
