using ProductService.Models;

namespace ProductService.Repository.Interfaces
{
    public interface IProductRepository
    {
        Task AddAsync(Product product);

        Task<List<Product>> GetAllAsync();

        Task<Product?> GetByIdAsync(int id);
    }
}
