# Shoko Server Plugin dir

This folder is copied to `plugins/` next to the server, where `PluginManager` loads the system plugins from. The
bundled plugins land in the same output folder, as `plugins/<project name>/`, copied there by `BundledPlugins.targets`
when `Shoko.CLI` or `Shoko.TrayService` is built. Every plugin found there is enabled by default and cannot be
uninstalled.

Plugins a user installs go to the `plugins` folder in the data directory instead.
