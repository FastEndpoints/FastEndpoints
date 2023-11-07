## Fixes 🪲

<details><summary>Tests couldn't assert on 'ProblemDetails' DTO due to having private property setters</summary>

When trying to assert on properties of `ProblemDetails` when testing, STJ could not deserialize the error JSON response due to the DTO having incorrect access modifiers for public properties, which has now been corrected.

</details>
