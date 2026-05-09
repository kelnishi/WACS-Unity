# Wacs.WasiNN.Sentis

`IBackend` implementation for the WACS `wasi-nn` host bindings, backed by
[Unity Sentis](https://docs.unity3d.com/Packages/com.unity.sentis@latest).
Lets WASM modules running on WACS in a Unity project drive ONNX inference
through the WASI-NN ABI without leaving the engine.

## Requirements

- `com.unity.sentis` ≥ 2.0.0 (asmdef gates `WACS_SENTIS` on this).
- The plugin DLLs `Wacs.WASI.NN.dll` + `Wacs.ComponentModel.dll` shipped in
  `Runtime/Plugins/`.

If Sentis is not installed, this asmdef compiles to nothing and the rest
of the package is unaffected.

## Wiring

```csharp
using Wacs.Core;
using Wacs.WASI.NN;
using Wacs.WASI.NN.Sentis;

var runtime = new WasmRuntime();

var nn = new WasiNNConfiguration
{
    Backends =
    {
        [GraphEncoding.ONNX] = new SentisBackend(),
    },
};
new WasiNNHost(nn).BindTo(runtime);

// Instantiate and run the wasi-nn guest as usual…
```

To pin every graph onto a specific Sentis backend (e.g. `GPUPixel` for
fragment-shader execution), pass it through the constructor:

```csharp
new SentisBackend(BackendType.GPUPixel)
```

## Encoding / target mapping

| wasi-nn                        | Sentis                  |
|--------------------------------|-------------------------|
| `GraphEncoding.ONNX`           | `ModelLoader.Load(...)` |
| `ExecutionTarget.CPU`          | `BackendType.CPU`       |
| `ExecutionTarget.GPU`          | `BackendType.GPUCompute`|
| `ExecutionTarget.TPU`          | `unsupported-operation` |

## v0 element-type support

| wasi-nn `TensorType`  | Sentis    | Status |
|-----------------------|-----------|--------|
| `FP32`                | `float`   | ✓      |
| `I32`                 | `int`     | ✓      |
| `FP16` / `BF16` / `FP64` / `U8` / `I64` | — | `unsupported-operation` |

Sentis 2.x exposes only `float` and `int` element types directly. Adding
the others is an open follow-up; the throw paths name what each requires.
