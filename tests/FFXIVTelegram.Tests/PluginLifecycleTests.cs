namespace FFXIVTelegram.Tests;

using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

public sealed class PluginLifecycleTests
{
    [Fact]
    public void WaitForPollingShutdownReturnsFalseWhenTaskDoesNotFinishBeforeTimeout()
    {
        var waitMethod = typeof(FfxivTelegramPlugin).GetMethod("WaitForPollingShutdown", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(waitMethod);

        var unfinishedTask = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task;
        var stopwatch = Stopwatch.StartNew();

        var completed = Assert.IsType<bool>(waitMethod!.Invoke(null, [unfinishedTask, TimeSpan.FromMilliseconds(50)]));

        stopwatch.Stop();

        Assert.False(completed);
        Assert.InRange(stopwatch.ElapsedMilliseconds, 25, 500);
    }

    [Fact]
    public void WaitForPollingShutdownTreatsCanceledTaskAsStopped()
    {
        var waitMethod = typeof(FfxivTelegramPlugin).GetMethod("WaitForPollingShutdown", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(waitMethod);

        var canceledTask = Task.FromCanceled(new CancellationToken(canceled: true));

        var completed = Assert.IsType<bool>(waitMethod!.Invoke(null, [canceledTask, TimeSpan.FromMilliseconds(50)]));

        Assert.True(completed);
    }
}
