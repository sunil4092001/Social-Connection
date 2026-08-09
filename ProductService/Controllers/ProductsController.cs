using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductService.DTO;
using ProductService.Services.Interfaces;

namespace ProductService.Controllers
{
    [ApiController]
    [Route("api/products")]
    public class ProductsController : ControllerBase
    {

        private readonly IProductService _service;

        public ProductsController(IProductService service)
        {
            _service = service;
        }

        [Authorize]
        [HttpPost("create")]
        public async Task<IActionResult> Create(
            ProductRequest request)
        {
            await _service.CreateAsync(request);

            return Ok(new
            {
                Message = "Product Created"
            });
        }

        [Authorize]
        [HttpGet("getAll")]
        public async Task<IActionResult> GetAll()
        {
            var products = await _service.GetAllAsync();

            return Ok(products);
        }
    }
  

}
