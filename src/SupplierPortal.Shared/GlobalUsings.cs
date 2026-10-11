// The Razor class-library SDK does not implicitly import the Microsoft.Extensions.* namespaces that the
// Web SDK does. The shared framework services (logging, configuration, DI, caching, options) rely on them,
// so import them globally here to match what these files had in the Web project.
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Caching.Memory;
global using Microsoft.Extensions.Options;
