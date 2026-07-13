using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace PhotoSort.Data.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly IDbContextFactory<PhotoSortDbContext> ContextFactory;

    public Repository(IDbContextFactory<PhotoSortDbContext> contextFactory)
    {
        ContextFactory = contextFactory;
    }

    public virtual async Task<T?> GetByIdAsync(int id)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<T>().FindAsync(id);
    }

    public virtual async Task<IReadOnlyList<T>> GetAllAsync()
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<T>().ToListAsync();
    }

    public virtual async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return await context.Set<T>().Where(predicate).ToListAsync();
    }

    public virtual async Task<T> AddAsync(T entity)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        await context.Set<T>().AddAsync(entity);
        await context.SaveChangesAsync();
        return entity;
    }

    public virtual async Task UpdateAsync(T entity)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        context.Set<T>().Update(entity);
        await context.SaveChangesAsync();
    }

    public virtual async Task DeleteAsync(T entity)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        context.Set<T>().Remove(entity);
        await context.SaveChangesAsync();
    }

    public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return predicate is null
            ? await context.Set<T>().CountAsync()
            : await context.Set<T>().CountAsync(predicate);
    }

    public virtual async Task<bool> AnyAsync(Expression<Func<T, bool>>? predicate = null)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();
        return predicate is null
            ? await context.Set<T>().AnyAsync()
            : await context.Set<T>().AnyAsync(predicate);
    }
}
