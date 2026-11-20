using System.Windows;
using System.Windows.Threading;

namespace EventsAPL; 

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. Ловим ошибки UI-потока
        this.DispatcherUnhandledException += App_DispatcherUnhandledException;

        // 2. Ловим ошибки фоновых потоков (async/await, Task)
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        // 3. Ловим ошибки, которые "потерялись" в асинхронщине
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ShowError("Ошибка интерфейса", e.Exception);
        e.Handled = true; // Говорим приложению: "Не падай, я справился"
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        ShowError("Фатальная ошибка системы", ex);
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        ShowError("Ошибка фоновой задачи (сеть/БД)", e.Exception);
        e.SetObserved(); // Гасим ошибку, чтобы не убить процесс
    }

    private void ShowError(string title, Exception? ex)
    {
        string message = ex?.Message ?? "Неизвестная ошибка";
        string stack = ex?.StackTrace ?? "";

        // Показываем ошибку в отдельном потоке, чтобы быть уверенными, что окно вылезет
        MessageBox.Show($"💥 {title}\n\n{message}\n\n--- Детали ---\n{stack}",
                        "EventsAPL - Сбой",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
    }
}