# Stateless: under-specified combinations

This document records interactions between Stateless mechanisms that cannot be established
from general state-machine knowledge or the README alone. "Under-specified" does not mean
the implementation has no definite behavior. These are observations of a specific version;
source references explain the results without turning implementation details into upstream
compatibility guarantees.

## Evidence and reproduction

- Verified on 2026-09-18.
- Loaded assembly: `Stateless 5.20.1+eff879c58baa1c8e40a4c994b2511fa9033f10ab`.
- Environment: .NET 10.0.8, Windows 10.0.26200. The probe reports its actual environment.
- Scope: `FiringMode.Queued`, synchronous hierarchy/guard/lifecycle interactions, and the
  explicitly identified asynchronous cases. Query scenarios do not modify state concurrently.
  Results do not establish behavior for `Immediate`, custom external state storage, or other
  unprobed combinations.
- The documentation comparison uses [this version's README][readme], rather than the short
  introduction included in the NuGet package.

The standalone [probe](../tools/stateless-probe/Program.cs) does not depend on Godot.
During restore, it reads the Stateless version directly from `Ciallo/Ciallo.csproj` and
requires that exact version without building the main project. With the .NET 10 SDK,
run from the repository root:

```sh
dotnet run --project tools/stateless-probe
dotnet run --project tools/stateless-probe -- --list
dotnet run --project tools/stateless-probe -- H02 Q02
```

Each ID identifies an independent experiment, sometimes with several contrasting scenarios.
Output includes visible state, callback order, guard evaluations, parameters, and exception
types. Key traces are compared with the 5.20.1 baseline below. `OBSERVED` means the observation
still matches that baseline. Exit code 0 means all selected experiments matched, 1 means a
difference or exception requires review, and 2 means invalid arguments.
After upgrading the dependency, rerun the probe and inspect the matching source version;
do not simply replace the expected results with new output. Raw output need not be committed.
This document is the authoritative record of the conclusions.

## H01: child guards and Ignore versus parent trigger acceptance

**Question:** The current state is `C`, with `C.SubstateOf(P)`. The parent handles `go` with
an internal action. Is declaring the same trigger in the child enough to intercept it?

| C's configuration for go | CanFire(go) | Actual handling of Fire(go) |
|---|---|---|
| No declaration | true | P |
| Internal action, guard=false | true | P |
| Internal action, guard=true | true | C |
| Ignore | true | No action; C consumes the event and P does not run |
| IgnoreIf, guard=false | true | P |

**Finding:** Declaration depth is not necessarily handler depth. A failed guard can fall
back to the parent. An applicable Ignore is a successful handling result, not `CanFire=false`.

**Implication for Ciallo:** After finding a child declaration through `GetInfo`, calling
`CanFire` on the whole machine can return true because of the parent. Ordering input candidates
by declaration depth must not be interpreted as ordering their effective handlers. An ignored
event also consumes input even though it causes no action or state change.
Source: [handler resolution][resolution].

## H02: inherited handlers, transition kinds, lifecycle, and State

**Minimal setup:** The current state is `C`. Both `C` and `D` are direct children of `P`,
and `P.InitialTransition(D)` is configured. Only P declares `go`. Compare six configurations,
observing child/parent entry and exit callbacks, both transition notifications, and `State`.

| P's configuration | Observed result |
|---|---|
| InternalTransition(go) | The action receives Transition `C→C`, with State=C. No entry, exit, transitioned, or completed callbacks. |
| Ignore(go) | No such callbacks; the final state remains C. |
| Permit(go, C) | When already in C, no such callbacks; the final state remains C. |
| PermitDynamic(go, () => C) | C exit → transitioned(C→C) → C entry → completed(C→C); P does not exit. |
| PermitReentry(go) | C exit → P exit → transitioned(P→P) → P entry → transitioned(P→D) → D entry → completed(P→P); State=D after Fire returns. |
| PermitDynamic(go, () => P) | C exit → transitioned(C→P) → transitioned(P→D) → D entry → completed(C→D); P neither exits nor enters, and the final state is D. |

The subtle point is **when parent reentry updates State**. In 5.20.1, `State` remains the
old child C until the completed callback finishes, and only then changes to D. A representative
trace is:

```text
exit C:       transition=C→P, State=C
exit P:       transition=P→P, State=C
entry P:      transition=P→P, State=C
entry D:      transition=P→D, State=C
completed:    transition=P→P, State=C
Fire returns: State=D
```

A dynamic transition to P follows the ordinary transition path instead: State is already P
at transitioned, remains P during D's entry, and is D at completed. Ending in the same initial
child does not imply that two configurations have the same lifecycle.

**Implication for Ciallo:** Code cannot universally assume that `State` is final inside a
completed callback, or that `Transition.Source` still identifies the original Interactor.
Property-tree refresh and shared-context synchronization that rely on those values must
distinguish ordinary transitions from parent reentry. The differences come from
[synchronous dispatch and the two transition paths][transition] together with
[hierarchical entry/exit][entry-exit]. The probe observes these differences without changing
application behavior to conceal them.

## H03: CanFire, guard reevaluation, and internal-action lookup

**Setup:** Both C and P declare guarded internal actions for go. Call CanFire while the child
guard is true, then change that guard's input to false before calling Fire.

CanFire returns true, but Fire executes P's action. Across this experiment the child guard runs
three times and the parent guard twice: CanFire performs one lookup, Fire resolves the handler
again, and internal-action execution performs another lookup.

**Implication for Ciallo:** `CanFire` is a query at that moment. It does not reserve a handler
or retain guard results for Fire. The exact counts are a version-specific experimental baseline,
not a once-per-event contract; a query result must not be cached as execution authorization.
Counters only observe evaluation here; a separate boolean controls handler selection.
Sources: [resolution][resolution] and [internal-action lookup][internal-action].

## Q01: Fire inside callbacks, queuing, and the state at the call site

**Setup:** A transitions to B. Only B has an internal action for pulse. Call `Fire(pulse)`
separately from A's exit, transitioned, B's entry, and completed.

In all four cases, the nested Fire returns before pulse executes. Pulse runs after the current
transition's completed callback, using B's rules. A's exit is particularly easy to misread:
`State=A` and `CanFire(pulse)=false`, but queuing the trigger directly succeeds later in B.

```text
A exit: State=A, CanFire(pulse)=false; Fire(pulse) returns
transitioned: State=B
B entry: State=B
completed: State=B
pulse action: State=B
outer Fire returns
```

**Implication for Ciallo:** Using CanFire inside a lifecycle callback to decide whether to
enqueue an event is not equivalent to publishing an event that the destination will accept
later. Enqueuing does not bind an event to the sender's current state; calling Fire from an
exit callback does not force the source state to handle it.
Source: [the synchronous queue and resolution at dequeue time][queue].

## Q02: InitialTransition, notifications, parameters, and queued work

**Setup:** `Fire(go, 7)` moves A to P, whose initial child is C. P's entry queues next;
only C accepts next, transitioning to Z.

Key observations:

```text
transitioned: A→P / go(7), State=P
P entry: A→P / go(7), State=P; enqueue next
transitioned: P→C / go(7), State=P
C entry: A→C / go(7), State=P
completed: A→C / go(7), State=C
then next: C→Z
```

The same go produces two transitioned notifications, and its argument reaches C's
`OnEntryFrom(go)`. However, the initial child's transitioned and entry callbacks see different
Source values, and reading State inside C's entry still returns P. Next is handled only after
initial entry completes.

**Implication for Ciallo:** Synchronizing context by trigger inside transitioned cannot assume
one notification per external event. Entry code also cannot determine whether its state has
been entered solely by reading State. Completed observes the final C for an ordinary A→P→C
transition, but parent reentry has the distinct behavior recorded in H02.
Source: [initial-transition recursion in EnterState][initial].

## Q03: queuing, mutable parameter objects, and guard timing

**Setup:** A's exit queues `work(payload)` with payload.Value=1 and the guard disabled.
Before that callback returns, it changes Value to 9 and enables the guard. B handles work,
using its parameter and guard to transition to C.

The guard sees Value=9 and enabled=true when B resolves the event. C's entry also receives 9.
The queue retains the parameter object reference; it does not snapshot the object's contents
or guard result at the call site.

**Implication for Ciallo:** If an event parameter represents a value at publication time,
choose an immutable value or snapshot at the business boundary. Passing a mutable container
and continuing to edit it does not preserve its previous contents for the receiver.
Source: [QueuedTrigger enqueue/dequeue][queue].

## Q04 / A02: callback exceptions, partial state changes, and pending events

**Setup:** A callback during A→B queues pending and then throws `ProbeFault`. After catching
it, the probe fires fresh to inspect the machine's remaining state. This is an observation,
not a recommendation to suppress application exceptions and continue.

| Throw site, synchronous Fire | State after the exception escapes | Has pending run? | Order on the next Fire(fresh) |
|---|---|---|---|
| A exit | A | No | pending@A → fresh@A |
| transitioned | B | No | pending@B → fresh@B |
| B entry | B | No | pending@B → fresh@B |
| completed | B | No | pending@B → fresh@B |

There is no transactional rollback or automatic clearing of the remaining queue. A02 exposes
a further difference: after an asynchronous B entry queues work and throws, the next
`FireAsync(fresh)` runs **fresh → pending**. Synchronous Fire first appends the new event to
the queue; an idle asynchronous entry point executes the new event before draining previously
queued work. This comparison retries through the same API in each case; mixed APIs were not probed.

**Implication for Ciallo:** Fire after a caught exception is not a clean retry. Earlier exit/entry
effects, the current State, and pending events may reflect a partially completed operation.
Exception recovery cannot consist solely of refiring the original trigger.
Sources: [synchronous queue][queue], [state assignment][transition], and [asynchronous queue][async-queue].

## I01: permitted-trigger queries, hierarchical masking, and ambiguity

**First setup:** C ignores go, while P declares go with a guard. `CanFire(go)` returns true
without evaluating P's guard. `GetPermittedTriggersAsync()` also lists go, but additionally
evaluates P's guard. Fire is ignored by C and never executes P's transition.

**Second setup:** One state declares go with two simultaneously true guards. Enumeration still
lists go; both CanFire and Fire throw `InvalidOperationException`.

**Implication for Ciallo:** Enumeration is not equivalent to calling CanFire on each trigger,
and its output is not a candidate list that has already passed ambiguity validation. It can
evaluate parent guards that normal dispatch never reaches. Query specific input candidates as
needed; an ignored trigger appearing in the permitted set does not imply a visible action.
Sources: [enumeration's Any and parent Union][permitted] and [single-handler ambiguity checks][resolution].

## I02: queries, parameterized triggers, and the guard execution thread

**Setup:** One state has guarded number(int) and text(string) triggers. The calling thread is
recorded as caller.

| Query/call | Observed guard inputs and result |
|---|---|
| CanFire("number") | The number guard receives 0 on caller. |
| CanFire(numberTrigger, 7) | Only the number guard runs, receiving 7 on caller. |
| Synchronous PermittedTriggers | The guards receive 0 and null, both on a thread other than caller. |
| GetPermittedTriggersAsync(), with only synchronous guards in this setup | The guards receive 0 and null, both on caller. |
| GetPermittedTriggersAsync(7) | The same argument list reaches the text guard's adapter and causes ArgumentException. |
| Raw Fire("number") with no argument | Parameter validation does not reject the empty list; the guard receives 0, rejects it, and the call ends with InvalidOperationException. |
| Raw Fire("value") with no argument, in a separate unguarded setup | The destination is entered successfully; OnEntryFrom receives int value 0. |

**Implication for Ciallo:** In this version, `PermittedTriggers` wraps the query in Task.Run,
which can move guards that read Godot objects off the main thread. The asynchronous API
completes synchronously in this experiment because there are no asynchronous guards; this
does not establish that all asynchronous queries remain on caller.
Parameterized business events should use the corresponding typed calls instead of relying
on raw Fire to reject every missing argument. Passing one argument to full enumeration is
also not equivalent to supplying each trigger with its own correctly typed arguments.

Sources: [synchronous query wrapper][sync-query], [shared enumeration arguments][permitted],
and [empty-argument conversion and validation][parameters].

## I03: GetInfo, declaration ownership, implicit initial edges, and snapshots

**Setup:** P initially enters C. Declare four triggers in the order dynamic, Ignore, internal,
static, then obtain GetInfo.

- GetInfo evaluates neither guards nor dynamic destination selectors.
- C's Transitions is empty; inherited behavior must be reached through Superstate. Effective
  parent actions are not copied into the child's declarations.
- P's Transitions contains internal, static, dynamic. Ignore appears separately in IgnoredTriggers.
- P→C's InitialTransition is not returned as a Transition in that collection.
- This reflection order differs from configuration order and is not an input-priority guarantee.
- After configuring an additional late internal action, the old info still has three Transitions
  and new info has four, while the current C can already accept late.

**Implication for Ciallo:** Input and substate caches built from reflection must distinguish
declarations from the runtime set of accepted triggers. Include the required categories,
traverse the hierarchy, and build the cache after configuration is complete. An existing info
object does not track subsequent configuration changes.
Sources: [GetInfo snapshot construction][info] and [StateInfo's grouping by category][state-info].

## A01: Queued mode, asynchronous entry, and nested await FireAsync

**Setup:** An outer `await FireAsync(go)` enters B. Inside B's asynchronous entry,
`await FireAsync(work)` enqueues work that B accepts.

```text
B entry begins
await FireAsync(work) returns; work has not executed
go's completed callback runs; work has not executed
work executes
outer await FireAsync(go) returns
```

**Implication for Ciallo:** If asynchronous lifecycle callbacks are introduced, completing a
nested await is not evidence that its queued action has finished. Here it only completes
enqueuing; the outer call continues draining the queue. The experiment makes no concurrent
calls and establishes no cross-thread safety guarantee.
Source: [the asynchronous queue returns after enqueuing while _firing][async-queue].

[readme]: https://github.com/dotnet-state-machine/stateless/blob/eff879c58baa1c8e40a4c994b2511fa9033f10ab/README.md
[resolution]: https://github.com/dotnet-state-machine/stateless/blob/eff879c58baa1c8e40a4c994b2511fa9033f10ab/src/Stateless/StateRepresentation.cs#L48
[internal-action]: https://github.com/dotnet-state-machine/stateless/blob/eff879c58baa1c8e40a4c994b2511fa9033f10ab/src/Stateless/StateRepresentation.cs#L231
[entry-exit]: https://github.com/dotnet-state-machine/stateless/blob/eff879c58baa1c8e40a4c994b2511fa9033f10ab/src/Stateless/StateRepresentation.cs#L173
[transition]: https://github.com/dotnet-state-machine/stateless/blob/eff879c58baa1c8e40a4c994b2511fa9033f10ab/src/Stateless/StateMachine.cs#L390
[initial]: https://github.com/dotnet-state-machine/stateless/blob/eff879c58baa1c8e40a4c994b2511fa9033f10ab/src/Stateless/StateMachine.cs#L514
[queue]: https://github.com/dotnet-state-machine/stateless/blob/eff879c58baa1c8e40a4c994b2511fa9033f10ab/src/Stateless/StateMachine.cs#L356
[async-queue]: https://github.com/dotnet-state-machine/stateless/blob/eff879c58baa1c8e40a4c994b2511fa9033f10ab/src/Stateless/StateMachine.Async.cs#L177
[permitted]: https://github.com/dotnet-state-machine/stateless/blob/eff879c58baa1c8e40a4c994b2511fa9033f10ab/src/Stateless/StateRepresentation.Async.cs#L164
[sync-query]: https://github.com/dotnet-state-machine/stateless/blob/eff879c58baa1c8e40a4c994b2511fa9033f10ab/src/Stateless/StateMachine.cs#L127
[parameters]: https://github.com/dotnet-state-machine/stateless/blob/eff879c58baa1c8e40a4c994b2511fa9033f10ab/src/Stateless/ParameterConversion.cs#L7
[info]: https://github.com/dotnet-state-machine/stateless/blob/eff879c58baa1c8e40a4c994b2511fa9033f10ab/src/Stateless/StateMachine.cs#L158
[state-info]: https://github.com/dotnet-state-machine/stateless/blob/eff879c58baa1c8e40a4c994b2511fa9033f10ab/src/Stateless/Reflection/StateInfo.cs#L38
