<div align="center">
	<h1>Dear Imgui Hook for Reloaded</h1>
	<img src="https://i.imgur.com/BjPn7rU.png" width="150" align="center" />
	<br/> <br/>
	<strong><i>Making life a bit easier.</i></strong><br/>
	<strong><i>Current ImGui Version: 1.88 Docking</i></strong><br/><br/>
	<img alt="Nuget" src="https://img.shields.io/nuget/v/Reloaded.Imgui.Hook">
	<br/>
</div>

# Introduction


Reloaded.Imgui.Hook is a simple personal use utility library that can be used to inject [Dear ImGui](https://github.com/ocornut/imgui) into the current process. 

It is based off of [DearImguiSharp](https://github.com/Sewer56/DearImguiSharp), my personal use bindings library for Dear ImGui built ontop of [cimgui](https://github.com/cimgui/cimgui).

<div align="center">
	<img src="./Images/Nepkopara.png" width="750" align="center" />
	<br/> 
	<i>Nekopara 0 by Neko Works</i>
	<br/> <br/>
</div>


## Basic Usage

Install `Reloaded.Imgui.Hook` from NuGet and/or the required implementations of your choice 
- Direct3D9: `Reloaded.Imgui.Hook.Direct3D9`
- Direct3D11: `Reloaded.Imgui.Hook.Direct3D11`
- OpenGL: `Reloaded.Imgui.Hook.OpenGL`

### Injecting the Overlay

First call `SDK.Init` to initialize all static variables used by the library, then simply call `ImguiHook.Create` with an appropriate delegate. 

```csharp
// During initialization.
SDK.Init(_hooks);
_imguiHook = await ImguiHook.Create(RenderTestWindow, new ImguiHookOptions()
{
	Implementations = new List<IImguiHook>()
    {
        new ImguiHookDx9(),   // `Reloaded.Imgui.Hook.Direct3D9`
        new ImguiHookDx11(),  // `Reloaded.Imgui.Hook.Direct3D11`
        new ImguiHookOpenGL() // `Reloaded.Imgui.Hook.OpenGL`
    }
}).ConfigureAwait(false);

private void RenderTestWindow()
{
	ImGuiNET.ImGui.ShowDemoWindow();
}
```

The `_hooks` variable is an instance of `IReloadedHooks` from [Reloaded.Hooks](https://github.com/Reloaded-Project/Reloaded.Hooks). In a [Reloaded-II](https://github.com/Reloaded-Project/Reloaded-II) mod, this is part of your mod template. Otherwise, add [Reloaded.Hooks](https://github.com/Reloaded-Project/Reloaded.Hooks) to your project and pass `new ReloadedHooks()` to the method.

### Enabling Docking Support
Use the specialised overload with `ImguiHookOptions`.

```csharp
await ImguiHook.Create(RenderTestWindow, new ImguiHookOptions()
{
    EnableViewports = true, // Enable docking.
    IgnoreWindowUnactivate = true // May help if game pauses when it loses focus.
}).ConfigureAwait(false);
```

### Enabling / Disabling the Overlay

```csharp
// Deactivate
_imguiHook.Disable();

// Activate
_imguiHook.Enable();
```
Leveraging  the capabilities of [Reloaded.Hooks](https://github.com/Reloaded-Project/Reloaded.Hooks), you can even unload your Assembly (DLL) from the process once you disable if you wish.

## Support
- D3D9
- D3D9Ex 
- D3D11
- OpenGL

Implemented backends should support window resizing and device resets (fullscreen/windowed switching) etc.  
PRs for supporting other backends (especially Vulkan) would be very welcome. My [DearImguiSharp](https://github.com/Sewer56/DearImguiSharp) does support them, so half of the work's already done.  

### In The Future
- Extensibility. Provide workarounds for possible edge cases such as SRGB colour space.

## Testing
If you would like to try the library, try the test mod for [Reloaded-II](https://github.com/Reloaded-Project/Reloaded-II) available in this repository.

Simply compile `Reloaded.ImGui.TestMod`, it will show up in your Reloaded-II's mod list.

## Known Issues
- [14 August 2021]: When viewports are Enabled and being used (window outside app/game window), destroying the hook instance (and this imgui context) may crash the application. I believe this a dear imgui issue.

## Contributions
Contributions are very welcome and encouraged; especially given that I'm not really a graphics programmer. I only have some experience in OpenGL 3 as opposed to DirectX 😉. 

Feel free to implement new features, make bug fixes or suggestions so long as they are accompanied by an issue with a clear description of the pull request.
