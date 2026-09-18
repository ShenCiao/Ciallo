using System.Reflection;
using Stateless;
using Machine = Stateless.StateMachine<string, string>;

var cases = new (string Id, string Question, Action<Probe> Run)[]
{
    ("H01", "Child guard failure versus Ignore: does the parent accept the trigger?", Scenarios.HierarchyResolution),
    ("H02", "Inherited transition kinds: which lifecycle hooks and visible states result?", Scenarios.InheritedLifecycle),
    ("H03", "Does CanFire reserve a handler, and how often do internal-transition guards run?", Scenarios.GuardReevaluation),
    ("Q01", "Queued Fire from exit/transitioned/entry/completed: which state handles it?", Scenarios.CallbackQueue),
    ("Q02", "Initial transition plus queued entry work: what do callbacks observe?", Scenarios.InitialEntryQueue),
    ("Q03", "Are queued arguments and guards captured at enqueue or dequeue?", Scenarios.QueuedPayload),
    ("Q04", "A callback throws with queued work pending: what state and queue survive?", Scenarios.ExceptionQueue),
    ("I01", "CanFire versus permitted-trigger enumeration: masking, Ignore and ambiguity?", Scenarios.QueryResolution),
    ("I02", "Querying parameterized guards: defaults, unrelated parameters and execution thread?", Scenarios.QueryParameters),
    ("I03", "GetInfo versus execution: inherited declarations, implicit edges and snapshots?", Scenarios.ReflectionSnapshot),
    ("A01", "Awaiting nested FireAsync in Queued mode: has that trigger executed?", Scenarios.AsyncQueue),
    ("A02", "After a callback failure, does FireAsync drain pending work before the new trigger?", Scenarios.AsyncExceptionQueue),
};

if (args.Contains("--list"))
{
    foreach (var scenario in cases) Console.WriteLine($"{scenario.Id}: {scenario.Question}");
    return 0;
}
if (args.Any(arg => !cases.Any(c => c.Id == arg)))
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/stateless-probe -- [--list | H01 Q01 ...]");
    return 2;
}

var assembly = typeof(Machine).Assembly;
Console.WriteLine($"Stateless {assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion}");
Console.WriteLine($"Runtime {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}; {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
Console.WriteLine("Baseline: Stateless 5.20.1. OBSERVED means the expected observations still match; it is not an upstream guarantee.");
int failures = 0;
foreach (var scenario in cases.Where(c => args.Length == 0 || args.Contains(c.Id)))
{
    Console.WriteLine($"\n[{scenario.Id}] {scenario.Question}");
    try
    {
        scenario.Run(new Probe());
        Console.WriteLine($"[{scenario.Id}] OBSERVED");
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine($"[{scenario.Id}] REVIEW: {exception}");
    }
}
Console.WriteLine($"\nReview required: {failures}");
return failures == 0 ? 0 : 1;

sealed class Probe
{
    public List<string> Events { get; } = [];

    public void Log(string text)
    {
        Events.Add(text);
        Console.WriteLine($"  {text}");
    }

    public void Expect<T>(string label, T actual, T expected)
    {
        Console.WriteLine($"  {label}: {actual}");
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            throw new InvalidOperationException($"{label}: expected {expected}, observed {actual}");
    }

    public static string Outcome(Action action)
    {
        try { action(); return "returned"; }
        catch (Exception exception) { return exception.GetType().Name; }
    }

    public void ExpectTrace(int first, params string[] expected)
    {
        var actual = Events.Skip(first).ToArray();
        if (!actual.SequenceEqual(expected))
            throw new InvalidOperationException($"Trace changed. Expected:\n{string.Join("\n", expected)}\nObserved:\n{string.Join("\n", actual)}");
    }
}

static class Scenarios
{
    public static void HierarchyResolution(Probe p)
    {
        foreach (string child in new[] { "absent", "guard-false", "guard-true", "ignore", "ignore-false" })
        {
            var m = new Machine("C", FiringMode.Queued);
            string handled = "none";
            m.Configure("P").InternalTransition("go", () => handled = "P");
            var c = m.Configure("C").SubstateOf("P");
            switch (child)
            {
                case "guard-false": c.InternalTransitionIf("go", _ => false, () => handled = "C"); break;
                case "guard-true": c.InternalTransitionIf("go", _ => true, () => handled = "C"); break;
                case "ignore": c.Ignore("go"); break;
                case "ignore-false": c.IgnoreIf("go", () => false); break;
            }
            p.Expect($"{child} CanFire", m.CanFire("go"), true);
            m.Fire("go");
            p.Expect($"{child} handler", handled, child == "guard-true" ? "C" : child == "ignore" ? "none" : "P");
            p.Expect($"{child} final state", m.State, "C");
        }
    }

    public static void InheritedLifecycle(Probe p)
    {
        foreach (string kind in new[] { "internal", "ignore", "reentry", "dynamic-parent", "static-child", "dynamic-child" })
        {
            p.Log($"-- {kind}");
            var m = new Machine("C", FiringMode.Queued);
            var parent = m.Configure("P").InitialTransition("D");
            m.Configure("C").SubstateOf("P");
            m.Configure("D").SubstateOf("P");
            switch (kind)
            {
                case "internal": parent.InternalTransition("go", t => p.Log($"internal {Describe(m, t)}")); break;
                case "ignore": parent.Ignore("go"); break;
                case "reentry": parent.PermitReentry("go"); break;
                case "dynamic-parent": parent.PermitDynamic("go", () => "P"); break;
                case "static-child": parent.Permit("go", "C"); break;
                case "dynamic-child": parent.PermitDynamic("go", () => "C"); break;
            }
            Watch(p, m, "P", "C", "D");
            int first = p.Events.Count;
            m.Fire("go");
            p.Expect("final", m.State, kind is "reentry" or "dynamic-parent" ? "D" : "C");
            if (kind is "ignore" or "static-child") p.Expect("callback count", p.Events.Count - first, 0);
            if (kind == "internal") p.ExpectTrace(first, "internal C>C/go, state=C, args=[]");
            switch (kind)
            {
                case "reentry":
                    p.ExpectTrace(first,
                        "exit C: C>P/go, state=C, args=[]",
                        "exit P: P>P/go, state=C, args=[]",
                        "transitioned: P>P/go, state=C, args=[]",
                        "entry P: P>P/go, state=C, args=[]",
                        "transitioned: P>D/go, state=C, args=[]",
                        "entry D: P>D/go, state=C, args=[]",
                        "completed: P>P/go, state=C, args=[]");
                    break;
                case "dynamic-parent":
                    p.ExpectTrace(first,
                        "exit C: C>P/go, state=C, args=[]",
                        "transitioned: C>P/go, state=P, args=[]",
                        "transitioned: P>D/go, state=P, args=[]",
                        "entry D: C>D/go, state=P, args=[]",
                        "completed: C>D/go, state=D, args=[]");
                    break;
                case "dynamic-child":
                    p.ExpectTrace(first,
                        "exit C: C>C/go, state=C, args=[]",
                        "transitioned: C>C/go, state=C, args=[]",
                        "entry C: C>C/go, state=C, args=[]",
                        "completed: C>C/go, state=C, args=[]");
                    break;
            }
        }
    }

    public static void GuardReevaluation(Probe p)
    {
        var m = new Machine("C", FiringMode.Queued);
        int childGuards = 0, parentGuards = 0;
        bool childEnabled = true;
        string handled = "none";
        m.Configure("P").InternalTransitionIf("go", _ => { parentGuards++; return true; }, () => handled = "P");
        m.Configure("C").SubstateOf("P")
            .InternalTransitionIf("go", _ => { childGuards++; return childEnabled; }, () => handled = "C");
        p.Expect("CanFire", m.CanFire("go"), true);
        p.Expect("child guards after CanFire", childGuards, 1);
        childEnabled = false;
        m.Fire("go");
        p.Expect("handler after guard changed", handled, "P");
        p.Expect("total child guard evaluations", childGuards, 3);
        p.Expect("total parent guard evaluations", parentGuards, 2);
    }

    public static void CallbackQueue(Probe p)
    {
        foreach (string phase in new[] { "exit", "transitioned", "entry", "completed" })
        {
            p.Log($"-- Fire from {phase}");
            var m = new Machine("A", FiringMode.Queued);
            string pulseState = "none";
            m.Configure("A").Permit("go", "B");
            m.Configure("B").InternalTransition("pulse", () => { pulseState = m.State; p.Log($"pulse state={m.State}"); });
            Watch(p, m, "A", "B");
            void Queue()
            {
                p.Log($"enqueue state={m.State}, CanFire(pulse)={m.CanFire("pulse")}");
                m.Fire("pulse");
                p.Expect("nested Fire returned before pulse", pulseState, "none");
            }
            switch (phase)
            {
                case "exit": m.Configure("A").OnExit(Queue); break;
                case "entry": m.Configure("B").OnEntry(Queue); break;
                case "transitioned": m.OnTransitioned(_ => Queue()); break;
                case "completed": m.OnTransitionCompleted(_ => Queue()); break;
            }
            int first = p.Events.Count;
            m.Fire("go");
            p.Expect("pulse handled in", pulseState, "B");
            var expected = new List<string>
            {
                "exit A: A>B/go, state=A, args=[]",
                "transitioned: A>B/go, state=B, args=[]",
                "entry B: A>B/go, state=B, args=[]",
                "completed: A>B/go, state=B, args=[]",
                "pulse state=B"
            };
            int insertion = Array.IndexOf(new[] { "exit", "transitioned", "entry", "completed" }, phase) + 1;
            expected.Insert(insertion, phase == "exit"
                ? "enqueue state=A, CanFire(pulse)=False"
                : "enqueue state=B, CanFire(pulse)=True");
            p.ExpectTrace(first, expected.ToArray());
        }
    }

    public static void InitialEntryQueue(Probe p)
    {
        var m = new Machine("A", FiringMode.Queued);
        var go = m.SetTriggerParameters<int>("go");
        m.Configure("A").Permit("go", "P");
        m.Configure("P").InitialTransition("C");
        m.Configure("C").SubstateOf("P").Permit("next", "Z");
        m.Configure("Z");
        Watch(p, m, "A", "P", "C", "Z");
        m.Configure("P").OnEntry(() => m.Fire("next"));
        string childVisibleState = "none";
        int argument = 0;
        m.Configure("C").OnEntryFrom(go, value => { childVisibleState = m.State; argument = value; });
        var completed = new List<string>();
        m.OnTransitionCompleted(t => completed.Add($"{t.Source}>{t.Destination}@{m.State}"));
        int first = p.Events.Count;
        m.Fire(go, 7);
        p.Expect("State read inside initial child's entry", childVisibleState, "P");
        p.Expect("initial child's inherited argument", argument, 7);
        p.Expect("completed then queued work", string.Join(",", completed), "A>C@C,C>Z@Z");
        p.Expect("final", m.State, "Z");
        p.ExpectTrace(first,
            "exit A: A>P/go, state=A, args=[7]",
            "transitioned: A>P/go, state=P, args=[7]",
            "entry P: A>P/go, state=P, args=[7]",
            "transitioned: P>C/go, state=P, args=[7]",
            "entry C: A>C/go, state=P, args=[7]",
            "completed: A>C/go, state=C, args=[7]",
            "exit C: C>Z/next, state=C, args=[]",
            "exit P: C>Z/next, state=C, args=[]",
            "transitioned: C>Z/next, state=Z, args=[]",
            "entry Z: C>Z/next, state=Z, args=[]",
            "completed: C>Z/next, state=Z, args=[]");
    }

    public static void QueuedPayload(Probe p)
    {
        var m = new Machine("A", FiringMode.Queued);
        var work = m.SetTriggerParameters<Payload>("work");
        var payload = new Payload { Value = 1 };
        bool enabled = false;
        int seen = 0;
        m.Configure("A").Permit("go", "B").OnExit(() =>
        {
            p.Log($"enqueue value={payload.Value}, guard={enabled}, state={m.State}");
            m.Fire(work, payload);
            payload.Value = 9;
            enabled = true;
        });
        m.Configure("B").PermitIf(work, "C", value => { p.Log($"guard value={value.Value}, enabled={enabled}, state={m.State}"); return enabled; });
        m.Configure("C").OnEntryFrom(work, value => seen = value.Value);
        m.Fire("go");
        p.Expect("value received", seen, 9);
        p.Expect("final", m.State, "C");
    }

    public static void ExceptionQueue(Probe p)
    {
        foreach (string phase in new[] { "exit", "transitioned", "entry", "completed" })
        {
            p.Log($"-- exception in {phase}");
            var m = new Machine("A", FiringMode.Queued);
            var actions = new List<string>();
            m.Configure("A").Permit("go", "B");
            foreach (string state in new[] { "A", "B" })
                m.Configure(state)
                    .InternalTransition("pending", () => actions.Add($"pending@{m.State}"))
                    .InternalTransition("fresh", () => actions.Add($"fresh@{m.State}"));
            void Fail() { m.Fire("pending"); throw new ProbeFault(); }
            switch (phase)
            {
                case "exit": m.Configure("A").OnExit(Fail); break;
                case "transitioned": m.OnTransitioned(_ => Fail()); break;
                case "entry": m.Configure("B").OnEntry(Fail); break;
                case "completed": m.OnTransitionCompleted(_ => Fail()); break;
            }
            p.Expect("first Fire", Probe.Outcome(() => m.Fire("go")), nameof(ProbeFault));
            string surviving = phase == "exit" ? "A" : "B";
            p.Expect("state after exception", m.State, surviving);
            p.Expect("pending work ran before exception escaped", actions.Count, 0);
            m.Fire("fresh");
            p.Expect("later Fire drains remaining queue", string.Join(",", actions), $"pending@{surviving},fresh@{surviving}");
        }
    }

    public static void QueryResolution(Probe p)
    {
        var m = new Machine("C");
        int parentGuards = 0;
        m.Configure("P").PermitIf("go", "Z", () => { parentGuards++; return true; });
        m.Configure("C").SubstateOf("P").Ignore("go");
        p.Expect("CanFire on child Ignore", m.CanFire("go"), true);
        p.Expect("CanFire evaluated masked parent guard", parentGuards, 0);
        p.Expect("listed despite Ignore", string.Join(",", m.GetPermittedTriggersAsync().GetAwaiter().GetResult()), "go");
        p.Expect("listing evaluated masked parent guard", parentGuards, 1);
        m.Fire("go");
        p.Expect("Fire on Ignore", m.State, "C");

        var ambiguous = new Machine("A");
        ambiguous.Configure("A").PermitIf("go", "B", () => true).PermitIf("go", "C", () => true);
        p.Expect("ambiguous trigger still listed", string.Join(",", ambiguous.GetPermittedTriggersAsync().GetAwaiter().GetResult()), "go");
        p.Expect("CanFire on ambiguity", Probe.Outcome(() => ambiguous.CanFire("go")), "InvalidOperationException");
        p.Expect("Fire on ambiguity", Probe.Outcome(() => ambiguous.Fire("go")), "InvalidOperationException");
    }

    public static void QueryParameters(Probe p)
    {
        var m = new Machine("A");
        var number = m.SetTriggerParameters<int>("number");
        var text = m.SetTriggerParameters<string>("text");
        int caller = Environment.CurrentManagedThreadId;
        var values = new List<string>();
        m.Configure("A")
            .PermitIf(number, "B", value => { values.Add($"number={value},caller={Environment.CurrentManagedThreadId == caller}"); return value > 0; })
            .PermitIf(text, "B", value => { values.Add($"text={value ?? "<null>"},caller={Environment.CurrentManagedThreadId == caller}"); return value == "yes"; });
        p.Expect("CanFire raw number", m.CanFire("number"), false);
        p.Expect("CanFire typed number=7", m.CanFire(number, 7), true);
        p.Expect("CanFire guard observations", string.Join(";", values), "number=0,caller=True;number=7,caller=True");
        values.Clear();
#pragma warning disable CS0618
        _ = m.PermittedTriggers.ToArray();
#pragma warning restore CS0618
        p.Expect("sync listing guard observations", string.Join(";", values), "number=0,caller=False;text=<null>,caller=False");
        values.Clear();
        _ = m.GetPermittedTriggersAsync().GetAwaiter().GetResult();
        p.Expect("async API with synchronous guards", string.Join(";", values), "number=0,caller=True;text=<null>,caller=True");
        p.Expect("one argument applied to heterogeneous guards",
            Probe.Outcome(() => m.GetPermittedTriggersAsync(7).GetAwaiter().GetResult()), "ArgumentException");
        p.Expect("raw Fire with missing number argument", Probe.Outcome(() => m.Fire("number")), "InvalidOperationException");

        var unguarded = new Machine("A");
        var valueTrigger = unguarded.SetTriggerParameters<int>("value");
        int received = -1;
        unguarded.Configure("A").Permit("value", "B");
        unguarded.Configure("B").OnEntryFrom(valueTrigger, value => received = value);
        p.Expect("raw Fire with missing unguarded argument", Probe.Outcome(() => unguarded.Fire("value")), "returned");
        p.Expect("missing argument received as", received, 0);
    }

    public static void ReflectionSnapshot(Probe p)
    {
        var m = new Machine("C");
        int guards = 0, selectors = 0;
        m.Configure("P").InitialTransition("C")
            .PermitDynamic("dynamic", () => { selectors++; return "Z"; })
            .Ignore("ignored")
            .InternalTransitionIf("internal", _ => { guards++; return true; }, () => { })
            .Permit("static", "Z");
        m.Configure("C").SubstateOf("P");
        var old = m.GetInfo();
        var parent = old.States.Single(s => (string)s.UnderlyingState == "P");
        var child = old.States.Single(s => (string)s.UnderlyingState == "C");
        p.Expect("GetInfo guards/selectors", $"{guards}/{selectors}", "0/0");
        p.Expect("child declarations exclude inherited triggers", child.Transitions.Count(), 0);
        p.Expect("parent transitions (reflection order)", string.Join(",", parent.Transitions.Select(t => t.Trigger.UnderlyingTrigger)), "internal,static,dynamic");
        p.Expect("Ignore stored separately", string.Join(",", parent.IgnoredTriggers.Select(t => t.Trigger.UnderlyingTrigger)), "ignored");
        p.Expect("initial edge is absent from Transitions", parent.Transitions.Any(t => t is Stateless.Reflection.FixedTransitionInfo f && (string)f.DestinationState.UnderlyingState == "C"), false);
        m.Configure("P").InternalTransition("late", () => { });
        p.Expect("old info is a snapshot", parent.Transitions.Count(), 3);
        p.Expect("new info includes late declaration", m.GetInfo().States.Single(s => (string)s.UnderlyingState == "P").Transitions.Count(), 4);
        p.Expect("current leaf can fire late inherited trigger", m.CanFire("late"), true);
    }

    public static void AsyncQueue(Probe p)
    {
        var m = new Machine("A", FiringMode.Queued);
        bool ran = false, observedAfterAwait = true;
        m.Configure("A").Permit("go", "B");
        m.Configure("B").OnEntryAsync(async () =>
        {
            await Task.Yield();
            p.Log($"entry before nested await, state={m.State}");
            await m.FireAsync("work");
            observedAfterAwait = ran;
            p.Log($"entry after nested await, workRan={ran}, state={m.State}");
        }).InternalTransitionAsync("work", () => { ran = true; p.Log("work executed"); return Task.CompletedTask; });
        m.OnTransitionCompleted(t => p.Log($"completed {Describe(m, t)}, workRan={ran}"));
        m.FireAsync("go").GetAwaiter().GetResult();
        p.Expect("nested await waited for handler", observedAfterAwait, false);
        p.Expect("outer await drained work", ran, true);
        p.ExpectTrace(0,
            "entry before nested await, state=B",
            "entry after nested await, workRan=False, state=B",
            "completed A>B/go, state=B, args=[], workRan=False",
            "work executed");
    }

    public static void AsyncExceptionQueue(Probe p)
    {
        var m = new Machine("A", FiringMode.Queued);
        var actions = new List<string>();
        m.Configure("A").Permit("go", "B");
        m.Configure("B").OnEntryAsync(async () =>
        {
            await m.FireAsync("pending");
            throw new ProbeFault();
        })
            .InternalTransition("pending", () => actions.Add("pending"))
            .InternalTransition("fresh", () => actions.Add("fresh"));
        p.Expect("first FireAsync", Probe.Outcome(() => m.FireAsync("go").GetAwaiter().GetResult()), nameof(ProbeFault));
        p.Expect("state after exception", m.State, "B");
        p.Expect("pending work ran before exception escaped", actions.Count, 0);
        m.FireAsync("fresh").GetAwaiter().GetResult();
        p.Expect("next FireAsync order", string.Join(",", actions), "fresh,pending");
    }

    private static string Describe(Machine m, Machine.Transition t) =>
        $"{t.Source}>{t.Destination}/{t.Trigger}, state={m.State}, args=[{string.Join(",", t.Parameters)}]";

    private static void Watch(Probe p, Machine m, params string[] states)
    {
        foreach (string state in states)
            m.Configure(state)
                .OnExit(t => p.Log($"exit {state}: {Describe(m, t)}"))
                .OnEntry(t => p.Log($"entry {state}: {Describe(m, t)}"));
        m.OnTransitioned(t => p.Log($"transitioned: {Describe(m, t)}"));
        m.OnTransitionCompleted(t => p.Log($"completed: {Describe(m, t)}"));
    }

    private sealed class Payload { public int Value { get; set; } }
    private sealed class ProbeFault : Exception { }
}
