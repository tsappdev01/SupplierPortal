namespace SupplierPortal.Web.Components.Shared;

/// <summary>State of a single row in <c>DiStatus</c> / a workflow item.</summary>
public enum DiStatusKind
{
    Processing,
    Pending,
    Completed,
    Failed,
    Paused,
    Retrying,
}

/// <summary>State of a phase in <c>DiMultiPhaseProgress</c>.</summary>
public enum DiPhaseState
{
    Pending,
    Active,
    Done,
}

/// <summary>One phase row for <c>DiMultiPhaseProgress</c>.</summary>
/// <param name="Name">Phase name, e.g. "Document Verification".</param>
/// <param name="State">Pending / Active / Done.</param>
/// <param name="Meta">Right-aligned note, e.g. a timestamp or "Pending".</param>
public record DiPhase(string Name, DiPhaseState State, string? Meta = null);
