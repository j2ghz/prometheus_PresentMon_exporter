using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Prometheus.Client;
using Prometheus.Client.MetricPusher;

namespace PresentMonToPrometheus
{
    public class Program
    {
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
            "ProcessID",
            //"SwapChainAddress",
            //"Runtime",
            //"SyncInterval",
            //"PresentFlags",
            //"AllowsTearing",
            //"PresentMode"
        };

        // 8 Dropped,
        private readonly Counter dropped = Metrics.CreateCounter("Dropped", "", false, labels);

        //11 MsBetweenDisplayChange,
        private readonly Counter msBetweenDisplayChange =
            Metrics.CreateCounter("MsBetweenDisplayChange", "", false, labels);

        //10 MsBetweenPresents,
        private readonly Counter msBetweenPresents = Metrics.CreateCounter("MsBetweenPresents", "", false, labels);

        //12 MsInPresentAPI,
        private readonly Counter msInPresentAPI = Metrics.CreateCounter("MsInPresentAPI", "", false, labels);

        //14 MsUntilDisplayed
        private readonly Counter msUntilDisplayed = Metrics.CreateCounter("MsUntilDisplayed", "", false, labels);

        //13 MsUntilRenderComplete,
        private readonly Counter msUntilRenderComplete =
            Metrics.CreateCounter("MsUntilRenderComplete", "", false, labels);

        // 9 TimeInSeconds,
        private readonly Gauge timeInSeconds = Metrics.CreateGauge("TimeInSeconds", "", false, labels);

        private readonly Counter frames =
            Metrics.CreateCounter("frames", "", false, labels);

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
            await input.ReadLineAsync();
            var pusher = new MetricPusher("http://192.168.0.2:9091", "PresentMon",Environment.MachineName);

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
                await pusher.PushAsync();
            }

        }

        private void updateMetrics(string[] fields)
        {
            var labelValues = fields[..2];

            frames.Labels(labelValues).Inc();
            dropped.Labels(labelValues).Inc(double.Parse(fields[8]));
            timeInSeconds.Labels(labelValues).Set(double.Parse(fields[9]));
            msBetweenPresents.Labels(labelValues).Inc(double.Parse(fields[10]));
            msBetweenDisplayChange.Labels(labelValues).Inc(double.Parse(fields[11]));
            msInPresentAPI.Labels(labelValues).Inc(double.Parse(fields[12]));
            msUntilRenderComplete.Labels(labelValues).Inc(double.Parse(fields[13]));
            msUntilDisplayed.Labels(labelValues).Inc(double.Parse(fields[14]));
        }
    }
}