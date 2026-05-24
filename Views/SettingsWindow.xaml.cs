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
                "탐색기 우클릭 → '연결 프로그램' 메뉴에 Deno Player가 나오고,\n" +
                "Windows '기본 앱' 화면에도 'Deno Player' 항목이 추가됐습니다.\n\n" +
                "지금 그 화면을 열까요? — Deno Player 항목 클릭 후 각 확장자를\n" +
                "Deno Player로 지정하면 더블클릭 시 우리 앱이 열립니다.\n\n" +
                "(Windows 정책상 기본 앱 변경은 사용자가 직접 눌러야 적용됩니다)",
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
