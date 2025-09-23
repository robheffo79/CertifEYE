using System;
using Microsoft.Extensions.Configuration;

#nullable enable

namespace AnchorSafe.API.Helpers
{
    /// <summary>
    /// Provides access to the application's configuration source.
    /// </summary>
    public static class ConfigurationHelper
    {
        private static IConfiguration? _configuration;

        /// <summary>
        /// Stores the application's <see cref="IConfiguration"/> instance for later retrieval.
        /// </summary>
        /// <param name="configuration">The configuration instance supplied by the host.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="configuration"/> is <c>null</c>.</exception>
        public static void Initialize(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Gets the initialized configuration instance.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="Initialize"/> has not been called.</exception>
        public static IConfiguration Configuration =>
            _configuration ?? throw new InvalidOperationException("ConfigurationHelper has not been initialized.");
    }
}
