---

## ⚠️ Goal Sponsorship Level Not Yet Met ⚠️

Please [join the discussion here](https://github.com/FastEndpoints/FastEndpoints/issues/1042) and help out if you can.

---

[//]: # (<details><summary>title text</summary></details>)

## Breaking Changes ⚠️

## New 🎉

<details><summary>Publish once to matching base-class and interface event handlers</summary>

If your events share a base class or interface, you can now deliver one publish to every compatible subscription:

```cs
app.Services.UseMessaging(o => o.Events.EnableInheritance = true);
app.UseFastEndpoints();
```

This is opt-in and also works with standalone messaging.

</details>

## Improvements 🚀

## Fixes 🪲