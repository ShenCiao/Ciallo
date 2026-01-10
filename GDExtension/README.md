# Ciallo GDExtension template

This folder contains a minimal C++ GDExtension scaffold for **Godot 4.5.1**.
It uses the official [`godot-cpp`](https://github.com/godotengine/godot-cpp) repository as a submodule pinned to the `4.5` branch.

## Getting started

1. Initialize the submodule:
   ```bash
   git submodule update --init --recursive
   ```
2. Build the extension with CMake:
   ```bash
   cmake -B build -S . -DCMAKE_BUILD_TYPE=Release
   cmake --build build
   ```
   The compiled library is copied into `bin/` next to this README.
3. Copy `ciallo.gdextension` into your Godot 4.5.1 project (keeping the `bin/` folder alongside it) and add the file to your project settings or autoloads as needed.

### Notes

- The example class `Example` registers a simple `say_hello()` method you can call from Godot for smoke testing.
- If you need debug symbols, build with `-DCMAKE_BUILD_TYPE=Debug` and update the library paths inside `ciallo.gdextension` if you place the files elsewhere.
