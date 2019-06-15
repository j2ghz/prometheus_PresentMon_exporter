using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Prometheus.Client;
using Prometheus.Client.MetricServer;

namespace PresentMonToPrometheus
{
    public class Program
    {
        private const string prefix = "presentmon_";

        // 0 Application,
        // 1 ProcessID,
        // 2 SwapChainAddress,
        // 3 Runtime,
        // 4 SyncInterval,
        // 5 PresentFlags,
        // 6 AllowsTearing,
        // 7 PresentMode,
        private static readonly string[] labels =
        {
            "Application",
            "ProcessID"
            //"SwapChainAddress",
            //"Runtime",
            //"SyncInterval",
            //"PresentFlags",
            //"AllowsTearing",
            //"PresentMode"
        };

        private static readonly double[] frametimesBuckets =
        {
            1,
            5,
            7,
            10,
            12.5,
            15,
            1000.0 / 60,
            20,
            25,
            1000.0 / 30,
            40,
            50,
            75,
            100,
            200,
            1000
        };

        public static double[] FrametimesBuckets => frametimesBuckets.Select(d=>d*1000).ToArray();

        // 8 Dropped,
        private readonly CounterInt64 dropped = Metrics.CreateCounterInt64(prefix + "frames_dropped", "Number of dropped frames", true, labels);

        private readonly Counter frames =
            Metrics.CreateCounter(prefix + "frames_count", "Frame counter (experimental)", true, labels);

        //11 MsBetweenDisplayChange,
        private readonly Histogram msBetweenDisplayChange =
            Metrics.CreateHistogram(prefix + "frametimes_display_between_ms", "MsBetweenDisplayChange", true,FrametimesBuckets, labels);

        //10 MsBetweenPresents,
        private readonly Histogram msBetweenPresents =
            Metrics.CreateHistogram(prefix + "frametimes_present_between_ms", "MsBetweenPresents", true, FrametimesBuckets, labels);

        //12 MsInPresentAPI,
        private readonly Histogram msInPresentAPI = Metrics.CreateHistogram(prefix + "frametimes_present_API_time_ms", "MsInPresentAPI", true, FrametimesBuckets, labels);

        //14 MsUntilDisplayed
        private readonly Histogram msUntilDisplayed =
            Metrics.CreateHistogram(prefix + "frametimes_display_delay_ms", "MsUntilDisplayed", true, FrametimesBuckets, labels);

        //13 MsUntilRenderComplete,
        private readonly Histogram msUntilRenderComplete =
            Metrics.CreateHistogram(prefix + "frametimes_render_time_ms", "MsUntilRenderComplete", true, FrametimesBuckets, labels);

        // 9 TimeInSeconds,
        private readonly Gauge timeInSeconds = Metrics.CreateGauge(prefix + "raw_TimeInSeconds", "TimeInSeconds", true, labels);

        private IMetricServer metricServer =
            new MetricServer(Metrics.DefaultCollectorRegistry, new MetricServerOptions {Port = 9091});

        public static async Task Main(string[] args)
        {
            Thread.CurrentThread.Name = "Main";
            var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                cancellationTokenSource.Cancel();
                eventArgs.Cancel = true;
            };

            var stdIn = args.Length == 1 ? File.OpenText(args[0]) : Console.In;
            Console.WriteLine("Running...\nPress Ctrl+C to exit");

            try
            {
                await new Program().Run(stdIn, cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Cancelled");
            }
            catch (Exception ex) when (!Debugger.IsAttached)
            {
                Console.Error.WriteLine("Unhandled Exception: " + ex.ToStringDemystified());
            }
        }

        public async Task Run(TextReader input, CancellationToken cancellationToken)
        {
            metricServer.Start();
            await input.ReadLineAsync();

            while (true)
            {
                if (cancellationToken.IsCancellationRequested) break;
                if (!(await input.ReadLineAsync() is { } line)) break;
                var fields = line.Split(",");
                try
                {
                    updateMetrics(fields);
                }
                catch (FormatException ex)
                {
                    Console.Error.WriteLine($"Couldn't parse line: '{line}'\n{ex.ToStringDemystified()}");
                }
            }
            metricServer.Stop();
        }

        private void updateMetrics(string[] fields)
        {
            var labelValues = fields[..2];

            frames.Labels(labelValues).Inc();
            dropped.Labels(labelValues).Inc(long.Parse(fields[8]));
            timeInSeconds.Labels(labelValues).Set(double.Parse(fields[9]));
            msBetweenPresents.Labels(labelValues).Observe(double.Parse(fields[10]));
            msBetweenDisplayChange.Labels(labelValues).Observe(double.Parse(fields[11]));
            msInPresentAPI.Labels(labelValues).Observe(double.Parse(fields[12]));
            msUntilRenderComplete.Labels(labelValues).Observe(double.Parse(fields[13]));
            msUntilDisplayed.Labels(labelValues).Observe(double.Parse(fields[14]));
        }
    }
}