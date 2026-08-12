# SDL3 diagnostics integration

Step 2 uses the `SDL3-CS` managed binding and the companion
`SDL3-CS.Windows` native package, both pinned to version `3.4.14.1`.
The binding is used only by the SDL diagnostic layer in this step; it
does not feed profiles, mappers, actions or output code.

The Windows native library is supplied by the `SDL3-CS.Windows` NuGet
package. The application currently builds for `x64`, so the project file
copies:

```text
runtimes\win-x64\native\SDL3.dll
```

from the package cache into the build and publish output. If the package
version is updated, update the pinned package references and the native
copy path together.

`SDL3-CS` is MIT licensed. SDL itself is zlib licensed. Keep those
licences in mind before redistributing updated native binaries.

The diagnostic display intentionally preserves SDL's raw button, axis,
touchpad and sensor identities. Do not add controller-family mappings or
universal-input policy here; production SDL-to-universal mapping belongs
to the backend adapter work.
