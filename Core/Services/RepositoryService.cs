using BetSniffer.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

public class RepositoryService<T> : IRepositoryService<T> where T : class
{
    protected readonly ApplicationDbContext _context;

    public RepositoryService(ApplicationDbContext context)
    {
        _context = context;
    }

    public T GetById(int id)
    {
        return _context.Set<T>().Find(id);
    }

    public IEnumerable<T> GetAll()
    {
        return _context.Set<T>().ToList();
    }

    public IEnumerable<T> Find(Expression<Func<T, bool>> expression)
    {
        return _context.Set<T>().Where(expression);
    }

    public void Add(T entity)
    {
        _context.Set<T>().Add(entity);
    }

    public void AddRange(IEnumerable<T> entities)
    {
        _context.Set<T>().AddRange(entities);
    }

    public void Remove(T entity)
    {
        _context.Set<T>().Remove(entity);
    }

    public void RemoveRange(IEnumerable<T> entities)
    {
        _context.Set<T>().RemoveRange(entities);
    }

    public void SaveOrUpdate(T entity)
    {
        var entry = _context.Entry(entity);
        if (entry.State == EntityState.Detached)
        {
            var primaryKey = _context.Model.FindEntityType(typeof(T)).FindPrimaryKey().Properties[0];
            var primaryKeyValue = primaryKey.PropertyInfo.GetValue(entity);

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

    public void SaveChanges()
    {
        Console.WriteLine("Salvando alterações no banco...");
        _context.SaveChanges();
        Console.WriteLine("Alterações salvas com sucesso.");
    }
}

