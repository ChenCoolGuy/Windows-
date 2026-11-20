using System.Diagnostics;

namespace TimerPower;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal sealed class MainForm : Form
{
    private readonly NumericUpDown hoursInput = new()
    {
        Minimum = 0,
        Maximum = 999,
        Width = 90
    };

    private readonly NumericUpDown minutesInput = new()
    {
        Minimum = 0,
        Maximum = 59,
        Width = 90
    };

    private readonly Label statusLabel = new()
    {
        AutoSize = true,
        Text = "设置时间后，选择一个操作。"
    };

    private readonly Button cancelButton = new()
    {
        Text = "取消计时",
        Width = 110,
        Enabled = false
    };

    private readonly System.Windows.Forms.Timer uiTimer = new()
    {
        Interval = 250
    };

    private Stopwatch? stopwatch;
    private TimeSpan scheduledDuration;
    private PowerAction scheduledAction;
    private bool actionStarted;

    public MainForm()
    {
        Text = "定时电源操作";
        ClientSize = new Size(390, 220);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        var hoursLabel = new Label
        {
            Text = "小时：",
            AutoSize = true,
            Location = new Point(28, 27)
        };
        hoursInput.Location = new Point(82, 23);

        var minutesLabel = new Label
        {
            Text = "分钟：",
            AutoSize = true,
            Location = new Point(195, 27)
        };
        minutesInput.Location = new Point(249, 23);

        var shutdownButton = new Button
        {
            Text = "关机",
            Location = new Point(28, 75),
            Size = new Size(100, 38)
        };
        shutdownButton.Click += (_, _) => StartCountdown(PowerAction.Shutdown);

        var hibernateButton = new Button
        {
            Text = "休眠",
            Location = new Point(145, 75),
            Size = new Size(100, 38)
        };
        hibernateButton.Click += (_, _) => StartCountdown(PowerAction.Hibernate);

        var restartButton = new Button
        {
            Text = "重启",
            Location = new Point(262, 75),
            Size = new Size(100, 38)
        };
        restartButton.Click += (_, _) => StartCountdown(PowerAction.Restart);

        statusLabel.Location = new Point(28, 137);
        cancelButton.Location = new Point(252, 165);
        cancelButton.Click += (_, _) => CancelCountdown();

        Controls.AddRange([
            hoursLabel, hoursInput,
            minutesLabel, minutesInput,
            shutdownButton, hibernateButton, restartButton,
            statusLabel, cancelButton
        ]);

        uiTimer.Tick += UpdateCountdown;
    }

    private void StartCountdown(PowerAction action)
    {
        var duration = TimeSpan.FromHours((double)hoursInput.Value)
                     + TimeSpan.FromMinutes((double)minutesInput.Value);

        if (duration <= TimeSpan.Zero)
        {
            MessageBox.Show(
                this,
                "请设置大于 0 的小时或分钟。",
                "时间无效",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        string actionName = ActionName(action);
        var confirmation = MessageBox.Show(
            this,
            $"将在 {duration.TotalHours:0} 小时 {duration.Minutes} 分钟后执行“{actionName}”。\n\n开始计时吗？",
            "确认定时操作",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirmation != DialogResult.Yes)
            return;

        scheduledAction = action;
        scheduledDuration = duration;
        stopwatch = Stopwatch.StartNew();
        actionStarted = false;

        hoursInput.Enabled = false;
        minutesInput.Enabled = false;
        cancelButton.Enabled = true;
        statusLabel.Text = $"距离{actionName}还有：{FormatRemaining(duration)}";

        uiTimer.Start();
    }

    private void UpdateCountdown(object? sender, EventArgs e)
    {
        if (stopwatch is null || actionStarted)
            return;

        var remaining = scheduledDuration - stopwatch.Elapsed;

        if (remaining > TimeSpan.Zero)
        {
            statusLabel.Text = $"距离{ActionName(scheduledAction)}还有：{FormatRemaining(remaining)}";
            return;
        }

        actionStarted = true;
        uiTimer.Stop();
        statusLabel.Text = $"正在执行：{ActionName(scheduledAction)}…";

        try
        {
            RunPowerAction(scheduledAction);
            statusLabel.Text = $"已发送“{ActionName(scheduledAction)}”命令。";
            cancelButton.Enabled = false;
        }
        catch (Exception ex)
        {
            ResetControls();
            MessageBox.Show(
                this,
                $"无法执行操作：{ex.Message}",
                "执行失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void CancelCountdown()
    {
        uiTimer.Stop();
        stopwatch?.Stop();
        stopwatch = null;

        ResetControls();
        statusLabel.Text = "计时已取消。";
    }

    private void ResetControls()
    {
        hoursInput.Enabled = true;
        minutesInput.Enabled = true;
        cancelButton.Enabled = false;
    }

    private static string FormatRemaining(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
            time = TimeSpan.Zero;

        int totalHours = (int)Math.Ceiling(time.TotalHours);
        int minutes = time.Minutes;
        int seconds = time.Seconds;

        return $"{totalHours} 小时 {minutes} 分 {seconds} 秒";
    }

    private static string ActionName(PowerAction action) => action switch
    {
        PowerAction.Shutdown => "关机",
        PowerAction.Hibernate => "休眠",
        PowerAction.Restart => "重启",
        _ => "操作"
    };

    private static void RunPowerAction(PowerAction action)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "shutdown.exe",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        switch (action)
        {
            case PowerAction.Shutdown:
                startInfo.ArgumentList.Add("/s");
                startInfo.ArgumentList.Add("/t");
                startInfo.ArgumentList.Add("0");
                break;

            case PowerAction.Restart:
                startInfo.ArgumentList.Add("/r");
                startInfo.ArgumentList.Add("/t");
                startInfo.ArgumentList.Add("0");
                break;

            case PowerAction.Hibernate:
                startInfo.ArgumentList.Add("/h");
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }

        Process.Start(startInfo)
            ?? throw new InvalidOperationException("未能启动 Windows 电源命令。");
    }

    private enum PowerAction
    {
        Shutdown,
        Hibernate,
        Restart
    }
}
