using Microsoft.EntityFrameworkCore;
using ProductService.Data;
using ProductService.Models;
using ProductService.Repository.Interfaces;

namespace ProductService.Repository.Implementation
{
    public class ProductRepository : IProductRepository
    {
        private readonly ProductDbContext _productDbContext;
        public ProductRepository(ProductDbContext productDbContext)
        {
            _productDbContext = productDbContext;

        }
        public async Task AddAsync(Product product)
        {
             _productDbContext.Products.Add(product);

            await _productDbContext.SaveChangesAsync();
        }

        public async Task<List<Product>> GetAllAsync()
        {
            return await _productDbContext.Products.ToListAsync();
        }

        public async Task<Product?> GetByIdAsync(int id)
        {
            return await _productDbContext.Products.FirstOrDefaultAsync(x => x.Id == id);
        }
    }
}
