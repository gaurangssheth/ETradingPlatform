using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PositionService.Infrastructure.Persistence;

namespace PositionService.Infrastructure.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {
        protected PositionDbContext context;
        protected DbSet<T> dbSet;
        
        public GenericRepository(PositionDbContext context)
        {
            this.context = context;
            dbSet = context.Set<T>();
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await dbSet.ToListAsync(cancellationToken);
        }

        public virtual async Task<T> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await dbSet.FindAsync(id, cancellationToken);
        }

        public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            var entityEntry = await dbSet.AddAsync(entity, cancellationToken);
            return entityEntry.Entity;
        }

        public virtual Task<T> UpsertAsync(T entity, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public virtual Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
