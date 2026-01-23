using BetSniffer.Api.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace BetSniffer.Api.Core.Services
{
    public class RepositoryService<T> : IRepositoryService<T> where T : class
    {
        protected readonly ApplicationDbContext _context;

        public RepositoryService(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public T? GetById(int id)
        {
            if (id <= 0)
                throw new ArgumentOutOfRangeException(nameof(id), "O ID deve ser maior que zero.");

            return _context.Set<T>().Find(id);
        }

        public IEnumerable<T> GetAll()
        {
            return _context.Set<T>().ToList();
        }

        public IEnumerable<T> Find(Expression<Func<T, bool>> expression)
        {
            if (expression == null)
                throw new ArgumentNullException(nameof(expression));
                
            return _context.Set<T>().Where(expression);
        }

        public void Add(T entity)
        {
            _ = entity ?? throw new ArgumentNullException(nameof(entity));
            _context.Set<T>().Add(entity);
        }

        public void AddRange(IEnumerable<T> entities)
        {
            _ = entities ?? throw new ArgumentNullException(nameof(entities));
            _context.Set<T>().AddRange(entities);
        }

        public void Remove(T entity)
        {
            _ = entity ?? throw new ArgumentNullException(nameof(entity));
            _context.Set<T>().Remove(entity);
        }

        public void RemoveRange(IEnumerable<T> entities)
        {
            _ = entities ?? throw new ArgumentNullException(nameof(entities));
            _context.Set<T>().RemoveRange(entities);
        }

        public void SaveOrUpdate(T entity)
        {
            _ = entity ?? throw new ArgumentNullException(nameof(entity));

            var entry = _context.Entry(entity);
            if (entry.State == EntityState.Detached)
            {
                var entityType = _context.Model.FindEntityType(typeof(T)) 
                    ?? throw new InvalidOperationException($"Não foi possível encontrar o tipo de entidade {typeof(T).Name} no modelo.");
                
                var primaryKey = entityType.FindPrimaryKey()?.Properties[0] 
                    ?? throw new InvalidOperationException($"Não foi possível encontrar a chave primária para a entidade {typeof(T).Name}.");

                var primaryKeyValue = primaryKey.PropertyInfo?.GetValue(entity);
                if (primaryKeyValue == null)
                {
                    throw new InvalidOperationException($"O valor da chave primária é nulo para a entidade {typeof(T).Name}.");
                }

                var existingEntity = _context.Set<T>().Find(primaryKeyValue);
                if (existingEntity == null)
                {
                    Add(entity);
                }
                else
                {
                    _context.Entry(existingEntity).CurrentValues.SetValues(entity);
                }
            }
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        public void SaveChanges()
        {
            _context.SaveChanges();
        }
    }
}
