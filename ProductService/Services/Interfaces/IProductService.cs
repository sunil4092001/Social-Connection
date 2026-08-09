using ProductService.DTO;
using ProductService.Models;

namespace ProductService.Services.Interfaces
{
    public interface IProductService
    {
        Task CreateAsync(ProductRequest request);

        Task<List<Product>> GetAllAsync();
    }
}
