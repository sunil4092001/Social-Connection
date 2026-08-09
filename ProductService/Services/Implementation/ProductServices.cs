using Microsoft.Win32;
using ProductService.DTO;
using ProductService.Models;
using ProductService.Repository.Interfaces;
using ProductService.Services.Interfaces;

namespace ProductService.Services.Implementation
{
    public class ProductServices : IProductService
    {
        private readonly IProductRepository _repository;

        public ProductServices(IProductRepository repository)
        {
            _repository = repository;
        }

        public async Task CreateAsync(
            ProductRequest request)
        {
            var product = new Product
            {
                Name = request.Name,
                Price = request.Price,
                Stock = request.Stock
            };

            await _repository.AddAsync(product);
        }

        public async Task<List<Product>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }
    }
   
}

