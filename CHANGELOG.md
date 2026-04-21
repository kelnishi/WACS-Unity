# Changelog

All notable changes to the unity package will be documented in this file.

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
