# ProGlass Agent — Teaching

## The safety property

A taught rule is **inert**. `IntentDetector.Detect` is only ever handed
`ActiveRulesAsync()` (approved rules). Teaching the agent something never
grants it a new behaviour until a human approves that specific rule.

## Grammar

```
"whenever I say <trigger>, <action>"
"remember that <trigger> means <action>"
```

Examples:

```
whenever I say stock check, show me the inventory
remember that morning report means show me recent activity
```

## Approval

```
approve rule 3
```

…or use the APPROVE button in Settings → AI & Agent.

## Actions are restricted

The free-text half is mapped by `IntentDetector.MapAction` into the action
DSL and **rejected if it does not map**:

```
navigate:<Target>        e.g. navigate:SheetStore
tool:<name>[:<query>]    e.g. tool:get_inventory
```

A rule therefore can never express something the agent could not already
do. It only creates a **new phrase for an existing capability**.

## Precedence

Approved rules are matched **last**, after all built-in intents. A taught
phrase can never shadow a system behaviour — teaching "help" to do
something else does not disable help.

## Lifecycle

| Operation | Effect | Audited |
|---|---|---|
| Propose | Stored, `IsApproved = false`, does nothing | yes |
| Approve | Becomes matchable | yes |
| Revoke | Deleted outright (not a soft flag) | yes |
| Clear | All rules removed | yes |
