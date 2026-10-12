using Microsoft.EntityFrameworkCore;

namespace SupplierPortal.Web.Data;

/// <summary>Context-agnostic factory for an <see cref="IAppDbContext"/>. Each app registers an
/// <see cref="AppDbContextFactory{TContext}"/> bound to its own concrete context, so services in
/// SupplierPortal.Shared can obtain a unit-of-work without knowing the concrete context type.</summary>
public interface IAppDbContextFactory
{
    Task<IAppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default);
    IAppDbContext CreateDbContext();
}

/// <summary>Adapts the EF Core <see cref="IDbContextFactory{TContext}"/> for a concrete context that
/// implements <see cref="IAppDbContext"/> to the context-agnostic <see cref="IAppDbContextFactory"/>.</summary>
public sealed class AppDbContextFactory<TContext>(IDbContextFactory<TContext> inner) : IAppDbContextFactory
    where TContext : DbContext, IAppDbContext
{
    public async Task<IAppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => await inner.CreateDbContextAsync(cancellationToken);

    public IAppDbContext CreateDbContext() => inner.CreateDbContext();
}
