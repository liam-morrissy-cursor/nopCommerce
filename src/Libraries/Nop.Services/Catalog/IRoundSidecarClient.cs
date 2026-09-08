using Nop.Core.Domain.Directory;

namespace Nop.Services.Catalog;

/// <summary>
/// Round sidecar client contract
/// </summary>
public interface IRoundSidecarClient
{
    /// <summary>
    /// Round a value using the Rust sidecar process
    /// </summary>
    decimal Round(decimal value, RoundingType roundingType);
}
