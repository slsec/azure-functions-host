﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Script.WebHost.Diagnostics
{
    internal class LinuxAppServiceEventGenerator : LinuxEventGenerator
    {
        private readonly Action<string> _writeEvent;
        private readonly LinuxAppServiceFileLoggerFactory _loggerFactory;
        private readonly HostNameProvider _hostNameProvider;

        public LinuxAppServiceEventGenerator(LinuxAppServiceFileLoggerFactory loggerFactory, HostNameProvider hostNameProvider, Action<string> writeEvent = null)
        {
            _writeEvent = writeEvent ?? WriteEvent;
            _loggerFactory = loggerFactory;
            _hostNameProvider = hostNameProvider ?? throw new ArgumentNullException(nameof(hostNameProvider));
        }

        public static string TraceEventRegex { get; } = "(?<Level>[0-6]),(?<SubscriptionId>[^,]*),(?<HostName>[^,]*),(?<AppName>[^,]*),(?<FunctionName>[^,]*),(?<EventName>[^,]*),(?<Source>[^,]*),\"(?<Details>.*)\",\"(?<Summary>.*)\",(?<HostVersion>[^,]*),(?<EventTimestamp>[^,]+),(?<ExceptionType>[^,]*),\"(?<ExceptionMessage>.*)\",(?<FunctionInvocationId>[^,]*),(?<HostInstanceId>[^,]*),(?<ActivityId>[^,\"]*)";

        public static string MetricEventRegex { get; } = "(?<SubscriptionId>[^,]*),(?<AppName>[^,]*),(?<FunctionName>[^,]*),(?<EventName>[^,]*),(?<Average>\\d*),(?<Min>\\d*),(?<Max>\\d*),(?<Count>\\d*),(?<HostVersion>[^,]*),(?<EventTimestamp>[^,]+),(?<Details>[^,\"]*)";

        public static string DetailsEventRegex { get; } = "(?<AppName>[^,]*),(?<FunctionName>[^,]*),\"(?<InputBindings>.*)\",\"(?<OutputBindings>.*)\",(?<ScriptType>[^,]*),(?<IsDisabled>[0|1])";

        public static string AzureMonitorEventRegex { get; } = $"{ScriptConstants.LinuxAzureMonitorEventStreamName} (?<Level>[0-6]),(?<ResourceId>[^,]*),(?<OperationName>[^,]*),(?<Category>[^,]*),(?<RegionName>[^,]*),\"(?<Properties>[^,]*)\",(?<EventTimestamp>[^,]+)";

        public static string ExecutionEventRegex { get; } = "(?<executionId>[^,]*),(?<siteName>[^,]*),(?<concurrency>[^,]*),(?<functionName>[^,]*),(?<invocationId>[^,]*),(?<executionStage>[^,]*),(?<executionTimeSpan>[^,]*),(?<success>[^,]*),(?<dateTime>[^,]*)";

        public override void LogFunctionTraceEvent(LogLevel level, string subscriptionId, string appName, string functionName, string eventName,
            string source, string details, string summary, string exceptionType, string exceptionMessage,
            string functionInvocationId, string hostInstanceId, string activityId, string runtimeSiteName, string slotName, DateTime eventTimestamp)
        {
            var formattedEventTimestamp = eventTimestamp.ToString(EventTimestampFormat);
            var hostVersion = ScriptHost.Version;
            var hostName = _hostNameProvider.Value;
            using (FunctionsSystemLogsEventSource.SetActivityId(activityId))
            {
                var logger = _loggerFactory.GetOrCreate(FunctionsLogsCategory);
                WriteEvent(logger, $"{(int)ToEventLevel(level)},{subscriptionId},{hostName},{appName},{functionName},{eventName},{source},{NormalizeString(details)},{NormalizeString(summary)},{hostVersion},{formattedEventTimestamp},{exceptionType},{NormalizeString(exceptionMessage)},{functionInvocationId},{hostInstanceId},{activityId}");
            }
        }

        public override void LogFunctionMetricEvent(string subscriptionId, string appName, string functionName, string eventName, long average,
            long minimum, long maximum, long count, DateTime eventTimestamp, string data, string runtimeSiteName, string slotName)
        {
            var hostVersion = ScriptHost.Version;
            var logger = _loggerFactory.GetOrCreate(FunctionsMetricsCategory);
            WriteEvent(logger, $"{subscriptionId},{appName},{functionName},{eventName},{average},{minimum},{maximum},{count},{hostVersion},{eventTimestamp.ToString(EventTimestampFormat)},{data}");
        }

        public override void LogFunctionDetailsEvent(string siteName, string functionName, string inputBindings, string outputBindings,
            string scriptType, bool isDisabled)
        {
            var logger = _loggerFactory.GetOrCreate(FunctionsDetailsCategory);
            WriteEvent(logger, $"{siteName},{functionName},{NormalizeString(inputBindings)},{NormalizeString(outputBindings)},{scriptType},{(isDisabled ? 1 : 0)}");
        }

        public override void LogFunctionExecutionAggregateEvent(string siteName, string functionName, long executionTimeInMs,
            long functionStartedCount, long functionCompletedCount, long functionFailedCount)
        {
        }

        public override void LogFunctionExecutionEvent(string executionId, string siteName, int concurrency, string functionName,
            string invocationId, string executionStage, long executionTimeSpan, bool success)
        {
            var logger = _loggerFactory.GetOrCreate(FunctionsExecutionEventsCategory);
            string currentUtcTime = DateTime.UtcNow.ToString();
            string log = string.Join(",", executionId, siteName, concurrency.ToString(), functionName, invocationId, executionStage, executionTimeSpan.ToString(), success.ToString(), currentUtcTime);
            WriteEvent(logger, log);
        }

        private static void WriteEvent(LinuxAppServiceFileLogger logger, string evt)
        {
            logger.Log(evt);
        }

        private void WriteEvent(string eventPayload)
        {
            Console.WriteLine(eventPayload);
        }

        public override void LogAzureMonitorDiagnosticLogEvent(LogLevel level, string resourceId, string operationName, string category, string regionName, string properties)
        {
            _writeEvent($"{ScriptConstants.LinuxAzureMonitorEventStreamName} {(int)ToEventLevel(level)},{resourceId},{operationName},{category},{regionName},{NormalizeString(properties.Replace("'", string.Empty))},{DateTime.UtcNow}");
        }

        public static void LogUnhandledException(Exception e)
        {
            // Pipe the unhandled exception to stdout as part of docker logs.
            Console.WriteLine($"Unhandled exception on {DateTime.UtcNow}: {e?.ToString()}");
        }
    }
}
