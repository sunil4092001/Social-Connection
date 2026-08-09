using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BuildingBlocks.Response
{
    public class ApiResponse<T>
    {
        public bool Success { get; init; }

        public string Message { get; init; } = string.Empty;

        public T? Data { get; init; }

        public IEnumerable<string>? Errors { get; init; }

        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        public string? CorrelationId { get; init; }//Useful for tracing requests across microservices.

        public int StatusCode { get; init; }
    }
}
