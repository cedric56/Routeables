using Routeables.Blazor.Services;

namespace Routeables.Tests.Razor;

/// <summary>
/// The dialog queue against a model, for random sequences of asking, finishing any dialog (the current one, a waiting one, one already
/// closed) and ending the circuit: the current dialog is always the oldest one still open, every dialog completes exactly once with the
/// result it was finished with, and the dialogs still open when the queue is disposed answer with their dismiss result.
/// </summary>
public class DialogQueueRandomizedTests
{
    private sealed class Request(int id, int dismiss) : IDialogRequest<int>
    {
        public int Id { get; } = id;
        public int Dismiss { get; } = dismiss;
        public TaskCompletionSource<int> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class Queue : DialogQueue<Request, int>
    {
        protected override int ResultWhenDisposed(Request request) => request.Dismiss;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public async Task The_queue_follows_the_model_for_any_sequence_of_calls(int seed)
    {
        var random = new Random(seed);
        using var queue = new Queue();
        queue.Changed += () => { };

        var all = new List<Request>();
        var open = new List<Request>();                 // oldest first
        var finished = new Dictionary<Request, int>();  // the result each closed request must have

        for (var step = 0; step < 300; step++)
        {
            if (all.Count == 0 || random.Next(3) == 0)
            {
                var request = new Request(all.Count, -random.Next(1, 5));
                all.Add(request);
                open.Add(request);
                _ = queue.EnqueueAsync(request, "RouteableDialogHost", "dialogs");
            }
            else
            {
                // Any request: open or not, current or waiting. A closed one must change nothing.
                var request = all[random.Next(all.Count)];
                var result = random.Next(10, 99);
                if (open.Remove(request)) finished[request] = result;
                queue.Finish(request, result);
            }

            Assert.Same(open.Count == 0 ? null : open[0], queue.Current);
            foreach (var request in all)
                Assert.Equal(finished.ContainsKey(request), request.Completion.Task.IsCompleted);
        }

        queue.Dispose();

        foreach (var request in all)
        {
            var result = await request.Completion.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Equal(finished.TryGetValue(request, out var wanted) ? wanted : request.Dismiss, result);
        }
    }
}
