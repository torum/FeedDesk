# Copilot Instructions

You are an expert Windows App SDK and WinUI 3 developer. 
Strictly follow these boundaries:

## General Guidelines
- Follow .github/copilot-instructions.md: WinUI 3/Microsoft.UI.Xaml, .NET 10 Windows TFM.
- Acknowledge the app supports unpackaged deployment. When initializing app startup logic outside of a package lifecycle, ensure the Windows App SDK Bootstrapper is explicitly managed or `<WindowsPackageType>None</WindowsPackageType>` configurations are respected.
- The app is AOT-compatible; code changes must remain compatible with trimming and Native AOT. Prefer compiled `x:Bind` in XAML and avoid reflection/dynamic patterns that cannot be statically analyzed.

## Code Style

### XAML formatting
- Keep each element's opening tag and all its attributes on a single line.
- Do not wrap an element's attributes across multiple lines.

### C# formatting
- Keep method, constructor, and lambda parameter lists on one line, even when the line is long.
- Do not put parameters on separate lines.
- Example: `private void Handle(object sender, EventArgs args)`.
- Keep argument lists on one line when practical.
- Do not wrap argument lists for formatting alone; wrap them only when keeping them on one line would make the code excessively long or hard to read.
- Prefer avoiding hard-coded enum member-name strings in parsers; use enum member ToString() or generic enum parsing so enum renames do not require updating duplicated string constants.


## WinUI 3 & Windows App SDK Architecture Guidelines (.NET 10)

### Core Ecosystem Rules
- **Target Framework:** Only target `.NET 10.0` with the Windows-specific Target Framework Moniker (e.g., `net10.0-windows10.0.19041.0`).
- **Namespaces:** Never output `Windows.UI.Xaml` (UWP). ALWAYS output `Microsoft.UI.Xaml` (WinUI 3)`.

### Deployment Strategy (Hybrid: Unpackaged & Packaged)
- **Conditional Guardrails:** When writing file I/O, local settings persistence, or protocol extensions, use conditional checks (`AppInstance.IsPackaged` equivalents or matching preprocessor directives) to gracefully fall back when running unpackaged.

### Window Management & Interop (Critical)
- **Window Handles (HWND):** WinUI 3 `Window` class does not have a direct `.hWnd` property. To get the window handle, always use `WinRT.Interop.WindowNative.GetWindowHandle(this)`.
- **UI Element App Windowing:** To get an `AppWindow` from a XAML `Window`, use the `Microsoft.UI.Xaml.Window.AppWindow` property or `Microsoft.UI.Win32Interop.GetAppWindowIdFromWindow(hWnd)`.
- **Content Dialogs:** Always explicitly set the `.XamlRoot` property when showing a `ContentDialog`, otherwise it will throw a runtime exception.

### MVVM Architecture (CommunityToolkit.Mvvm)
- **Source Generators:** Do not write `INotifyPropertyChanged` boilerplate.
- **Properties:** Use `[ObservableProperty]` on private fields (using `camelCase` or `_camelCase`) so the Toolkit generates public `PascalCase` properties when the generated property requires no custom getter or setter logic. For properties that require custom logic, implement them manually and call ` SetProperty(ref field, value)` in the setter instead of using [ObservableProperty].
- **Commands:** Use `[RelayCommand]` on methods instead of creating `ICommand` properties manually.
- Use `ObservableRecipient` or `Messenger` patterns for decoupled View-Model communication.

### Threading & Concurrency
- **UI Thread Access:** If updating the UI from a background thread or asynchronous task, never use `Dispatcher.RunAsync` (UWP legacy). Use `this.DispatcherQueue.TryEnqueue(() => { ... })` or equivalent custom method like IDispatcherService.TryEnqueue.
- **Asynchronous Execution:** Prefer `async` / `await` over synchronous blocks or `.Result`. Ensure async events use `await` properly inside UI lifecycle methods.

### XAML Controls & Styling
- Prefer modern WinUI 3 native controls over legacy custom implementations: `InfoBar`, `NumberBox`, `TeachingTip`, `ItemContainer`, and `PipsPager`.
- Utilize **ThemeResources** (e.g., `ThemeResource SystemControlPageBackgroundChromeLowBrush`) for seamless native Dark/Light mode switching instead of hardcoded hex colors.

### Fields
- For fields, prefer typed enums defined in Models over string constants/options declared in XAML.






