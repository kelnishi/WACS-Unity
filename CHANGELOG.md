# Changelog

All notable changes to the unity package will be documented in this file.

## [0.10.0]
- Bumps Wacs.Core to 0.10.0 (matches the latest core runtime release)
- WASI Preview 1 has been renamed: the bundled `Wacs.WASIp1.dll` is replaced by `Wacs.WASI.Preview1.dll` (0.11.0). New WASI capabilities since 0.8.2: full sock_* surface (accept / recv / send / shutdown), real path_link / path_symlink (gated on AllowHardLinks / AllowSymbolicLinks), fd_fdstat_set_flags / fd_fdstat_set_rights honored, fd_seek u64 fix, poll_oneoff clock-id and write-readiness fixes, plus 100% conformance against `WebAssembly/wasi-testsuite` (43 / 72 fixtures pass; the rest are documented skips).
- Network sockets gated behind a default-off `WasiConfiguration.AllowNetworkSockets` flag plus the requirement that the embedder hand WACS pre-bound, pre-listening sockets via the new `PreopenedSockets` config list.
- New bundled `Wacs.HostBindings.Abstractions.dll` (0.1.0) — attribute API ([WacsImport], [WacsImportNames], etc.) for binding host functions. Used by Wacs.WASI.Preview1; available for custom host libraries that want to follow the same declarative pattern.
- Bumps `Microsoft.Extensions.ObjectPool.dll` to 9.0.0.

### Migration from 0.8.2

- Replace `using Wacs.WASIp1;` with `using Wacs.WASI.Preview1;` in any sample or project script that bound to WASI Preview 1. The `Wacs.Core.WASIp1` namespace inside `Wacs.Core.dll` (`IBindable`, `ErrNo`, `SystemExitException`, etc.) is unchanged.
- The bundled `Wacs.WASIp1.dll` is removed; restart the Unity Editor after upgrading so the asset database picks up the new DLLs.

## [0.8.2]
- First-class WAT / WAST text-format parser in Wacs.Core — load `.wat` modules directly, no pre-compile step
- New Wacs.Core.Components namespace: WIT IDL parser (component-model groundwork)
- Spec-suite parity: 100% of WebAssembly 3.0 core `.wast` files parse and match the binary parser structurally
- Picks up 0.8.0 / 0.8.1 runtime additions previously skipped in Unity:
  - Source-generated switch runtime (opt-in via `runtime.UseSwitchRuntime = true`) — AOT-safe, IL2CPP-friendly, no `Reflection.Emit`. Resolves `System.Runtime.CompilerServices.Unsafe` from Unity's built-in BCL (no new plugin DLL shipped)
  - Bytecode-stream super-instruction fuser for the switch runtime

## [0.7.3]
- Reimplemented AOT compatible invoker bindings

## [0.7.2]
- removing Linq.Expression for AOT compatibility

## [0.7.1]
- fixes to CreateInvoker binding

## [0.7.0]
- wasm-3.0 spec support
- exnref/tag support
- memory64 support
- multi-memory support (enabled)

## [0.6.0]
- wasm-gc extension
- function-references extension

## [0.3.0]
- Implemented JSPI-like async binding and execution
- Hooked up more super-instruction threading

## [0.2.0]
- Implemented super-instruction threading
- Precomputed (non-allocating) block labels

## [0.1.6]
- Updating to latest dll
- Fixing package layout
- Fixing Sample importer

## [0.1.4]
### Added
- Initial project setup for Unity.

### Changed
- Updated project structure to allow installation as a Unity package from git.
