using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace HaoKhiSuViet.Tests
{
    /// <summary>
    /// Test-only instrumentation for <see cref="GameEventBus"/>, done entirely by
    /// reflection so production code carries no tracing hooks.
    ///
    /// • Publication trace: subscribes a logging handler to every bus event and
    ///   writes "PUBLISH &lt;Event&gt; field=value ... timeScale=x" lines.
    /// • Subscription trace: <see cref="Snapshot"/> reads each event's backing
    ///   delegate and lists its handlers, so tests can diff subscribers
    ///   before/after and detect leaks or double subscriptions.
    ///
    /// Handlers declared in the HaoKhiSuViet.Tests namespace (this tracer, test
    /// rigs, lambdas in tests) are excluded from snapshots by default.
    /// </summary>
    public sealed class EventBusTracer : IDisposable
    {
        private const BindingFlags StaticAny = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const string TestNamespacePrefix = "HaoKhiSuViet.Tests";
        private const int MaxStringLength = 60;

        private static readonly EventInfo[] BusEvents = typeof(GameEventBus).GetEvents(StaticAny);
        private static readonly Dictionary<Type, FieldInfo[]> PayloadFields = new Dictionary<Type, FieldInfo[]>();

        private readonly List<KeyValuePair<EventInfo, Delegate>> _handlers = new List<KeyValuePair<EventInfo, Delegate>>();

        /// <summary>Number of events published since this tracer attached.</summary>
        public int PublishCount { get; private set; }

        /// <summary>Attaches a logging handler to every GameEventBus event.</summary>
        public EventBusTracer() => Attach();

        /// <summary>
        /// Re-attaches handlers. Call after <see cref="GameEventBus.Reset"/>, which
        /// removes the tracer along with every other subscriber.
        /// </summary>
        public void Attach()
        {
            Detach();
            MethodInfo open = typeof(EventBusTracer).GetMethod(nameof(OnPublished), BindingFlags.Instance | BindingFlags.NonPublic);

            foreach (EventInfo busEvent in BusEvents)
            {
                Type payload = busEvent.EventHandlerType.GetGenericArguments()[0];
                Delegate handler = Delegate.CreateDelegate(busEvent.EventHandlerType, this, open.MakeGenericMethod(payload));
                busEvent.AddEventHandler(null, handler);
                _handlers.Add(new KeyValuePair<EventInfo, Delegate>(busEvent, handler));
            }
        }

        /// <summary>Removes the tracer's handlers from the bus.</summary>
        public void Dispose() => Detach();

        private void Detach()
        {
            foreach (KeyValuePair<EventInfo, Delegate> pair in _handlers)
                pair.Key.RemoveEventHandler(null, pair.Value);
            _handlers.Clear();
        }

        private void OnPublished<T>(T payload)
        {
            PublishCount++;
            RegressionLog.Info("EventBus", $"PUBLISH {typeof(T).Name} {Describe(payload)} timeScale={Time.timeScale:0.##}");
        }

        // ─────────────────────────────────────────────────────────────────────
        // SUBSCRIBER SNAPSHOTS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns event name → handler names ("Type.Method") for every event
        /// with at least one subscriber.
        /// </summary>
        public static SortedDictionary<string, List<string>> Snapshot(bool includeTestHandlers = false)
        {
            var snapshot = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (EventInfo busEvent in BusEvents)
            {
                // Field-like static events are backed by a static field of the same name.
                FieldInfo backing = typeof(GameEventBus).GetField(busEvent.Name, StaticAny);
                if (!(backing?.GetValue(null) is Delegate multicast)) continue;

                var names = new List<string>();
                foreach (Delegate handler in multicast.GetInvocationList())
                {
                    Type owner = handler.Method.DeclaringType;
                    bool isTestHandler = owner?.Namespace != null && owner.Namespace.StartsWith(TestNamespacePrefix, StringComparison.Ordinal);
                    if (isTestHandler && !includeTestHandlers) continue;

                    names.Add($"{owner?.Name}.{handler.Method.Name}");
                }

                if (names.Count > 0) snapshot[busEvent.Name] = names;
            }

            return snapshot;
        }

        /// <summary>Total handler count across a snapshot.</summary>
        public static int CountHandlers(SortedDictionary<string, List<string>> snapshot)
        {
            int total = 0;
            foreach (List<string> handlers in snapshot.Values) total += handlers.Count;
            return total;
        }

        /// <summary>Number of non-test handlers currently subscribed to <paramref name="eventName"/>.</summary>
        public static int CountHandlers(string eventName, string handlerMethod = null)
        {
            if (!Snapshot().TryGetValue(eventName, out List<string> handlers)) return 0;
            if (handlerMethod == null) return handlers.Count;

            int count = 0;
            foreach (string handler in handlers)
                if (handler.EndsWith("." + handlerMethod, StringComparison.Ordinal)) count++;
            return count;
        }

        /// <summary>
        /// Describes subscriber changes between two snapshots, one line per
        /// changed event (e.g. "OnOngButAnswerResult 0→1 [+EraProgressionManager.HandleAnswerResult]").
        /// Returns an empty list when nothing changed.
        /// </summary>
        public static List<string> Diff(SortedDictionary<string, List<string>> before, SortedDictionary<string, List<string>> after)
        {
            var lines = new List<string>();
            var eventNames = new SortedSet<string>(before.Keys, StringComparer.Ordinal);
            eventNames.UnionWith(after.Keys);

            foreach (string eventName in eventNames)
            {
                List<string> was = before.TryGetValue(eventName, out List<string> b) ? b : new List<string>();
                List<string> now = after.TryGetValue(eventName, out List<string> a) ? a : new List<string>();

                var changes = new StringBuilder();
                AppendMultisetDifference(now, was, '+', changes);
                AppendMultisetDifference(was, now, '-', changes);
                if (changes.Length == 0) continue;

                lines.Add($"{eventName} {was.Count}→{now.Count} [{changes}]");
            }

            return lines;
        }

        /// <summary>Writes every subscribed event and its handlers to the log.</summary>
        public static void LogSnapshot(string label, SortedDictionary<string, List<string>> snapshot)
        {
            RegressionLog.Info("Subscribers", $"{label}: {CountHandlers(snapshot)} handler(s) on {snapshot.Count} event(s)");
            foreach (KeyValuePair<string, List<string>> pair in snapshot)
                RegressionLog.Info("Subscribers", $"  {pair.Key} x{pair.Value.Count}: {string.Join(", ", pair.Value)}");
        }

        // Appends items present in `source` more times than in `other`.
        private static void AppendMultisetDifference(List<string> source, List<string> other, char sign, StringBuilder into)
        {
            var remaining = new List<string>(other);
            foreach (string item in source)
            {
                if (remaining.Remove(item)) continue;
                if (into.Length > 0) into.Append(", ");
                into.Append(sign).Append(item);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // PAYLOAD FORMATTING
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Formats a struct payload as "Field=value Field=value".</summary>
        public static string Describe(object payload)
        {
            if (payload == null) return "(null)";

            Type type = payload.GetType();
            if (!PayloadFields.TryGetValue(type, out FieldInfo[] fields))
            {
                fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
                PayloadFields[type] = fields;
            }

            if (fields.Length == 0) return "{}";

            var text = new StringBuilder();
            foreach (FieldInfo field in fields)
            {
                if (text.Length > 0) text.Append(' ');
                text.Append(field.Name).Append('=').Append(FormatValue(field.GetValue(payload)));
            }
            return text.ToString();
        }

        private static string FormatValue(object value)
        {
            switch (value)
            {
                case null:
                    return "null";
                case UnityEngine.Object unityObject:
                    return unityObject == null ? "null(destroyed)" : $"'{unityObject.name}'";
                case string text:
                    string oneLine = text.Replace('\n', ' ').Replace('\r', ' ');
                    return oneLine.Length > MaxStringLength ? $"\"{oneLine.Substring(0, MaxStringLength)}…\"" : $"\"{oneLine}\"";
                case Array array:
                    return $"[{array.Length}]";
                case float number:
                    return number.ToString("0.###");
                default:
                    return value.ToString();
            }
        }
    }
}
