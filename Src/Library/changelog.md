
---

### ✨ Looking For Sponsors ✨

FastEndpoints needs sponsorship to [sustain the project](https://github.com/FastEndpoints/FastEndpoints/issues/449). Please help out if you can.

---

<!-- <details><summary>title text</summary></details> -->

## 📢 New

<details><summary>Ability to auto instantiate 'JsonSerializerContext' with global 'SerializerOptions'</summary>

```cs
public override void Configure()
{
    ...
    SerializerContext<UpdateAddressCtx>();
}
```

By specifying just the type of the serializer context, instead of supplying an instance as with the existing method, the context will be created using the `SerializerOptions` that you've configured at startup using the `UseFastEndpoints(...)` call.

</details>

## 🚀 Improvements

<details><summary>Minor performance optimizations</summary>

- Job queue message pump improvements

</details>

<!-- ## 🪲 Fixes -->

<!-- ## ⚠️ Minor Breaking Changes -->